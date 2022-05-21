using System.Net;
using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;
using NtpClient;

using FatKATT;
using NSDotnet;
using NSDotnet.Models;

#region License
/*
FattKATT Manual triggering tool
Copyright (C) 2022 Vleerian R

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
#endregion

var app = new CommandApp<FattKATTCommand>();
return await app.RunAsync(args);

class FattKATTCommand : AsyncCommand<FattKATTCommand.Settings>
{
    const string VersionNumber = "1.3.2";

    int PollSpeed = 750;
    NtpConnection ntpConnection;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-n|--nation"), Description("The nation using FattKATT")]
        public string? Nation { get; init; }

        [CommandOption("-t|--triggers"), Description("A comma-separated list of triggers to use instead of trigger_list.txt")]
        public string? Triggers { get; init; }

        [CommandOption("-p|--poll-speed"), Description("Poll speed, minimum is 750")]
        public int? PollSpeed { get; init; }

        [CommandOption("-b|--beep"), Description("If you want FattKATT to beep when a target updates")]
        public bool? Beep { get; init; }
    }

    public async override Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
    {

        // Set up NSDotNet
        var API = NSAPI.Instance;
        API.UserAgent = $"FatKATT/{VersionNumber} (By 20XX, Atagait@hotmail.com)";
        
        // The splash was cut out of the main function for the sake of readability
        PrintSplash();

        // Fetch version information from github
        Logger.Request("Checking for newer versions...");
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"FattKATT/{VersionNumber} (https://github.com/vleerian/fattkatt)");
            var gitReq = await httpClient.GetAsync("https://api.github.com/repos/vleerian/fattkatt/releases/latest");
            var versionInfo = await gitReq.Content.ReadFromJsonAsync<GithubAPI>();
            int result = CompareVersion(versionInfo!.Tag_Name!, VersionNumber);
            switch(result)
            {
                case 0: Logger.Info("A newer version of FattKATT has been released https://github.com/Vleerian/FattKATT/releases/latest"); break;
                case 1: Logger.Warning("You are using a bleeding-edge build of FattKATT, it is reccommended to use the latest official release."); break;
                case 2: Logger.Info("FattKATT is up to date!"); break;
                case 3: Logger.Warning("Invalid semantic versioning - it is recommended to use the latest official release."); break;
                case 4: Logger.Warning("You are using an experimental build, here be dragons."); break;
            }
            httpClient.Dispose();
        }

        #region UserIdentification
        string? UserNation = settings.Nation;
        if(settings.Nation == null)
        {
            AnsiConsole.WriteLine("FatKATT requires your nation to inform NS Admin who is using it.");
            UserNation= AnsiConsole.Ask<string>("Please provide your [green]nation[/]: ");
        }
        var r = await API.MakeRequest($"https://www.nationstates.net/cgi-bin/api.cgi?nation={Helpers.SanitizeName(UserNation)}");
        if(r.StatusCode != HttpStatusCode.OK)
        {
            // An error message specifically for 404 since that is the result of a user error
            if(r.StatusCode == HttpStatusCode.NotFound)
                Logger.Error("The provided nation does not exist.");
            else
                Logger.Error($"NS replied with {(int)r.StatusCode}: {r.StatusCode.ToString()}, shutting down");
            return 1;
        }
        int rl = CheckRatelimit(r);
        if ( rl > 10 )
            Logger.Warning($"The API has recieved {rl} requests from you.");

        NationAPI Nation = Helpers.BetterDeserialize<NationAPI>(await r.Content.ReadAsStringAsync());
        API.UserAgent = $"FatKATT/{VersionNumber} (By 20XX, Atagait@hotmail.com - In Use by {UserNation})";
        Logger.Info($"You have identified as {Nation.fullname}.");
        #endregion

        if(settings.PollSpeed != null)
            PollSpeed = settings.PollSpeed >= 750 ? (int)settings.PollSpeed : 750;
        else
            PollSpeed = AnsiConsole.Prompt(new TextPrompt<int>("How many miliseconds should KATT wait between NS API requests? ")
                .DefaultValue(750)
                .ValidationErrorMessage("[red]Invalid poll speed.[/]")
                .Validate(s => s switch {
                    < 600 => ValidationResult.Error("[red]Poll speed too low. Minimum 600[/]"),
                    _ => ValidationResult.Success(),
                    })
                );

        bool Beep;
        if(settings.Beep != null)
            Beep = (bool)settings.Beep;
        else
            Beep = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Enable Beeping?")
                .AddChoices(new[] { "Yes", "No" })) == "Yes" ;

        #region PreProcessTriggerList
        List<string> Triggers = null;
        while(Triggers == null)
        {
            string[] triggers;
            if(settings.Triggers == null)
            {
                Logger.Processing("Loading trigger regions from trigger_list.txt");
                if(!File.Exists("./trigger_list.txt"))
                {
                    File.WriteAllText("./trigger_list.txt", "#trigger_list.txt\n#format is 1 trigger region per line.\n#lines can be commented out with hash marks.");
                    Logger.Info("File does not exist. Template created, please populate trigger_list.txt with list of trigger regions.");
                    Console.WriteLine("Press ENTER to continue."); Console.ReadLine();
                }
                triggers = File.ReadAllText("./trigger_list.txt").Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                triggers = triggers.Select(L=>L.Trim().Replace(' ','_')).Where(L => !L.StartsWith("#")).ToArray();
                if(triggers.Length == 0)
                {
                    Logger.Error("Trigger list is empty. Please populate trigger_list.txt with list of trigger regions.");
                    Console.WriteLine("Press ENTER to continue."); Console.ReadLine();
                }
                Triggers = triggers.ToList();
            }
            else
            {
                Triggers = settings.Triggers.Split(',')
                    .Select(T=>Helpers.SanitizeName(T))
                    .ToList();
            }
        }
        #endregion

        // The NTP timeserver is used because some people apparently don't have their system clock set properly
        ntpConnection = new NtpConnection("pool.ntp.org");
        int current_time = CurrentTimestamp();

        // Fetch the lastupdate data for all the regions in the trigger list
        Logger.Info("Sorting triggers.");
        List<(double timestamp, string trigger)> Sorted_Triggers = new();
        foreach (string trigger in Triggers)
        {
            Logger.Request($"Getting LastUpdate for {trigger}");
            var req = await API.MakeRequest($"https://www.nationstates.net/cgi-bin/api.cgi?region={trigger}&q=lastupdate+name");
            if(req.StatusCode != HttpStatusCode.OK)
            {
                Logger.Error($"Failed to fetch data for {trigger}. It will not be checked for updates.");
                continue;
            }

            RegionAPI Region = Helpers.BetterDeserialize<RegionAPI>(await req.Content.ReadAsStringAsync());
            if(Region.LastUpdate == null)
                Logger.Warning($"{trigger} is a new region.");
            else if (current_time - Region.LastUpdate < 7200)
                Logger.Warning($"{trigger} has already updated.");
            else
                Sorted_Triggers.Add((Region.LastUpdate, trigger));
        }
        // Sort all the triggers by their lastupdate
        Sorted_Triggers.Sort((x, y) => x.timestamp.CompareTo(y.timestamp));
        Logger.Info($"Sorted {Sorted_Triggers.Count} triggers.");

        await AnsiConsole.Progress()
            .AutoClear(false)
            .AutoRefresh(true)
            .HideCompleted(false)
            .Columns(new ProgressColumn[] {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx => {

            ProgressTask ProgTask = ctx.AddTask("Waiting for next update...", maxValue: Sorted_Triggers.Count);
            while(Sorted_Triggers.Count > 0)
            {
                var Trigger = Sorted_Triggers.First();
                ProgTask.Description = $"Waiting for {Trigger.trigger}";
                RegionAPI Region;
                try {
                    await Task.Delay(PollSpeed);
                    var req = await API.MakeRequest($"https://www.nationstates.net/cgi-bin/api.cgi?region={Trigger.trigger}&q=lastupdate");
                    if(req.StatusCode == HttpStatusCode.NotFound)
                    {
                        Logger.Warning("Target cannot be found, skipping");
                        ProgTask.Increment(1.0);
                        Sorted_Triggers.Remove(Trigger);
                    }
                    Region = Helpers.BetterDeserialize<RegionAPI>(await req.Content.ReadAsStringAsync());
                }
                catch ( HttpRequestException e )
                {
                    // Error handling for rate limit being exceeded, in the case that an exception is thrown
                    if(e.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        Logger.Warning("Rate limit exceeded. Sleeping for 5 seconds.");
                        Thread.Sleep(5000);
                    }
                    else
                        Logger.Warning("Error loading region data ");
                    break;
                }

                if(Trigger.timestamp != Region.LastUpdate)
                {
                    AnsiConsole.MarkupLine($"[red]!!![/] - [yellow]UPDATE DETECTED IN {Trigger.trigger}[/] - [red]!!![/]");
                    if(Beep)
                        Console.Beep();
                    ProgTask.Increment(1.0);
                    Sorted_Triggers.Remove(Trigger);
                }
            }
        });

        Logger.Info("All targets have updated, shutting down.");
        Console.WriteLine("Press ENTER to continue."); Console.ReadLine();

        return 0;
    }

    /// <summary>
    /// This method compares two semantic versioning tags to check which is higher
    /// <returns type="int">0 if VerisonA is higher, 
    /// 1 if VersionB is higher.
    /// 2 if they are the same
    /// 3 if there are less than 3 integer parts to the version
    /// 4 if the version parts themselves are invalid</returns>
    /// </summary>
    int CompareVersion(string VersionA, string VersionB)
    {
        // (StringSplitOptions)3 is shorthand for trim entries and remove empty entries
        var PartsA = VersionA.Split('.', 3, (StringSplitOptions)3);
        if(PartsA.Length < 3)
            return 3;
        var PartsB = VersionB.Split('.', 3, (StringSplitOptions)3);
        if(PartsB.Length < 3)
            return 3;
        return CompareVersionParts(PartsA, PartsB, 0);
    }

    /// <summary>
    /// This method compares parts of thw semantic versioning tags to check which is higher
    /// <returns type="bool">True if VerisonA is higher, false if VersionB is higher</returns>
    /// </summary>
    int CompareVersionParts(string[] PartsA, string[] PartsB, int index)
    {
        int ResultA, ResultB;
        if(index >= PartsA.Length)
            return 2;
        else if(!Int32.TryParse(PartsA[index], out ResultA))
            return 4;
        else if(!Int32.TryParse(PartsB[index], out ResultB))
            return 4;
        if(ResultA > ResultB)
            return 0;
        else if(ResultA < ResultB)
            return 1;
        return CompareVersionParts(PartsA, PartsB, ++index);
    }

    /// <summary>
    /// This method checks the X-ratelimit-requests-seen header and returns the value
    /// </summary>
    int CheckRatelimit(HttpResponseMessage r)
    {
        string strRatelimitSeen = r.Headers.GetValues("X-ratelimit-requests-seen").First();
        return Int32.Parse(strRatelimitSeen);
    }

    /// <summary>
    /// I am told that a shocking number of people do not have their system time properly set
    /// To that end, I poll the current UTC time through NTP to ensure accuracy.
    /// <returns>The current epoch timestamp</returns>
    /// </summary>
    int CurrentTimestamp()
    {
        var utcNow = ntpConnection.GetUtc(); 
        TimeSpan t = utcNow - new DateTime(1970, 1, 1);
        return (int)t.TotalSeconds;
    }

    public void PrintSplash()
    {
        AnsiConsole.MarkupLine("[red]██╗  ██╗ █████╗ ████████╗████████╗[/]");
        AnsiConsole.MarkupLine("[red]██║ ██╔╝██╔══██╗╚══██╔══╝╚══██╔══╝[/]");
        AnsiConsole.MarkupLine("[red]█████╔╝ ███████║   ██║      ██║   [/]");
        AnsiConsole.MarkupLine("[red]██╔═██╗ ██╔══██║   ██║      ██║   [/]");
        AnsiConsole.MarkupLine("[red]██║  ██╗██║  ██║   ██║      ██║   [/]");
        AnsiConsole.MarkupLine("[red]╚═╝  ╚═╝╚═╝  ╚═╝   ╚═╝      ╚═╝   [/]");
        AnsiConsole.WriteLine("Khron and Atagait's Triggering Tool");
        AnsiConsole.WriteLine("        |\\___/|");
        AnsiConsole.WriteLine("        )     (  ");
        AnsiConsole.WriteLine("       =\\     /=");
        AnsiConsole.WriteLine("         )===(   ");
        AnsiConsole.WriteLine("        /     \\");
        AnsiConsole.WriteLine("        |     |");
        AnsiConsole.WriteLine("       /       \\");
        AnsiConsole.WriteLine("       \\       /");
        AnsiConsole.WriteLine("_/\\_/\\_/\\__  _/_/\\_/\\_/\\_/\\_/\\_/\\_");
        AnsiConsole.WriteLine("|  |  |  |( (  |  |  |  |  |  |  |");
        AnsiConsole.WriteLine("|  |  |  | ) ) |  |  |  |  |  |  |");
        AnsiConsole.WriteLine("|  |  |  |(_(  |  |  |  |  |  |  |");
        AnsiConsole.WriteLine("|  |  |  |  |  |  |  |  |  |  |  |");
        AnsiConsole.WriteLine("|  |  |  |  |  |  |  |  |  |  |  |");
        AnsiConsole.WriteLine($"FatKATT Version {VersionNumber}.");
        AnsiConsole.WriteLine($"This software is provided as-is, without warranty of any kind.");
    }
}

[Serializable]
public class GithubAPI
{
    [JsonPropertyName("tag_name")]
    public string? Tag_Name { get; init; }

    [JsonPropertyName("name")]
    public string? Release_Name { get; init; }

    [JsonPropertyName("published_at")]
    public string? published { get; init; }

    [JsonIgnore]
    public DateTime Published => DateTime.Parse(published);
}