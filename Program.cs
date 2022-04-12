using System.Net;
using System.Net.Sockets;
using System.Xml.Serialization;
using Spectre.Console;
using FatKATT;
using NtpClient;

#region License
/*
Copyright 2022 Vleerian R.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is furnished
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

const string VersionNumber = "1.1.2";

HttpClient client = new();
client.DefaultRequestHeaders.Add("User-Agent", $"FatKATT/{VersionNumber} (By 20XX, Atagait@hotmail.com)");
int PollSpeed = 750;

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

AnsiConsole.WriteLine("FatKATT requires your nation to inform NS Admin who is using it.");

string Nation_Name = AnsiConsole.Ask<string>("Please provide your [green]nation[/]: ");
NationData Nation;
try {
    var r = await MakeReq($"https://www.nationstates.net/cgi-bin/api.cgi?nation={Sanitize(Nation_Name)}");
    int rl = CheckRatelimit(r);
    if ( rl > 10 )
        Logger.Warning($"The API has recieved {rl} requests from you.");
    Nation = BetterDeserialize<NationData>(await r.Content.ReadAsStringAsync());
    if(Nation == null)
    {
        Logger.Error($"{Nation_Name} does not exist.");
        return;
    }
} catch (HttpRequestException e)
{
    Logger.Error($"Failed to fetch data for nation {Nation_Name}", e);
    return;
}
client.DefaultRequestHeaders.Remove("User-Agent");
client.DefaultRequestHeaders.Add("User-Agent", $"FatKATT/{VersionNumber} (By 20XX, Atagait@hotmail.com - In Use by {Nation_Name})");
Logger.Info($"You have identified as {Nation.fullname}.");

PollSpeed = AnsiConsole.Prompt(new TextPrompt<int>("How many miliseconds should KATT wait between NS API requests? ")
    .DefaultValue(750)
    .ValidationErrorMessage("[red]Invalid poll speed.[/]")
    .Validate(s => s switch {
        < 600 => ValidationResult.Error("[red]Poll speed too low. Minimum 600[/]"),
        _ => ValidationResult.Success(),
        })
    );

List<string> Triggers = null;
while(Triggers == null)
{
    Logger.Processing("Loading trigger regions from trigger_list.txt");
    if(!File.Exists("./trigger_list.txt"))
    {
        File.WriteAllText("./trigger_list.txt", "#trigger_list.txt\n#format is 1 trigger region per line.\n#lines can be commented out with hash marks.");
        Logger.Info("File does not exist. Template created, please populate trigger_list.txt with list of trigger regions.");
        Console.WriteLine("Press ENTER to continue."); Console.ReadLine();
    }
    string[] triggers = File.ReadAllLines("./trigger_list.txt");
    triggers = triggers.Where(L => !L.StartsWith("#")).ToArray();
    if(triggers.Length == 0)
    {
        Logger.Error("Trigger list is empty. Please populate trigger_list.txt with list of trigger regions.");
        Console.WriteLine("Press ENTER to continue."); Console.ReadLine();
    }
    Triggers = triggers.ToList();
}

bool Beep = AnsiConsole.Prompt(new SelectionPrompt<string>()
    .Title("Enable Beeping?")
    .AddChoices(new[] { "Yes", "No" })) == "Yes" ;

var connection = new NtpConnection("pool.ntp.org");

int current_time = CurrentTimestamp();
List<(int timestamp, string trigger)> Sorted_Triggers = new();
foreach (string trigger in Triggers)
{
    try{
        var req = await MakeReq($"https://www.nationstates.net/cgi-bin/api.cgi?region={trigger}&q=lastupdate+name");
        int rl = CheckRatelimit(req);

        if(req.StatusCode != HttpStatusCode.OK)
        {
            Logger.Error($"Failed to fetch nation data for {trigger}");
            Triggers.Remove(trigger);
            continue;
        }

        var Region = BetterDeserialize<RegionData>(await req.Content.ReadAsStringAsync());
        if(Region == null)
        {
            Logger.Warning($"{trigger} does not exist.");
            Triggers.Remove(trigger);
        }
        else if (current_time - Region.lastupdate < 7200)
        {
            Logger.Warning($"{trigger} has already updated.");
        }
        else
            Sorted_Triggers.Add((Region.lastupdate, trigger));
    }
    catch (HttpRequestException e)
    {
        Logger.Warning($"Failed to fetch data on {trigger} - removing.", e);
        Triggers.Remove(trigger);
    }
}
Sorted_Triggers.Sort((x, y) => x.timestamp.CompareTo(y.timestamp));

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
    Dictionary<string, ProgressTask> Tasks = new();
    foreach ( var trigger in Sorted_Triggers )
        Tasks.Add(trigger.trigger, ctx.AddTask(trigger.trigger, maxValue: 1.0));
    
    while(!ctx.IsFinished)
    {
        foreach ( var trigger in Sorted_Triggers )
        {
            RegionData Region;
            try {
                var req = await MakeReq($"https://www.nationstates.net/cgi-bin/api.cgi?region={trigger.trigger}&q=lastupdate+name");
                // Error handling for rate limit being exceeded
                if(req.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    int RetAfter = Int32.Parse(req.Headers.GetValues("X-Retry-After").First());
                    Logger.Warning($"Rate limit exceeded. Sleeping for {RetAfter*2}");
                    Thread.Sleep(RetAfter*2);
                    break;
                }
                Region = BetterDeserialize<RegionData>(await req.Content.ReadAsStringAsync());
            }
            catch ( HttpRequestException e )
            {
                // Error handling for rate limit being exceeded, in the case that an exception is thrown
                if(e.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Logger.Warning("Rate limit exceeded. Sleeping for 30 seconds.");
                    Thread.Sleep(30000);
                    break;
                }
                Logger.Warning("Error loading region data - trying again.");
                break;
            }
            
            if(trigger.timestamp != Region.lastupdate)
            {
                AnsiConsole.MarkupLine($"[red]!!![/] - [yellow]UPDATE DETECTED IN {trigger.trigger}[/] - [red]!!![/]");
                if(Beep)
                    Console.Beep();
                Tasks[trigger.trigger].Increment(1.0);
                Sorted_Triggers.Remove(trigger);
                break;
            }
        }
    }
});

Logger.Info("All targets have updated, shutting down.");
Console.WriteLine("Press ENTER to continue."); Console.ReadLine();

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
    var utcNow = connection.GetUtc(); 
    TimeSpan t = utcNow - new DateTime(1970, 1, 1);
    return (int)t.TotalSeconds;
}

/// <summary>
/// This method removes capital letters and converts spaces to underscore
/// <param name="text">The text to be sanitized</param>
/// <returns>An all-lowercase string with spaces converted to underscores</returns>
/// </summary>
string Sanitize(string text) => text.ToLower().Replace(' ', '_');

/// <summary>
/// This method waits the delay set by the program, then makes a request
/// <param name="url">The URL to request from</param>
/// <returns>The return from the request.</returns>
/// </summary>
async Task<HttpResponseMessage> MakeReq(string url) {
    System.Threading.Thread.Sleep(PollSpeed);
    return await client.GetAsync(url);
}

/// <summary>
/// This method makes deserializing XML less painful
/// <param name="url">The URL to request from</param>
/// <returns>The parsed return from the request.</returns>
/// </summary>
T BetterDeserialize<T>(string XML) =>
    (T)new XmlSerializer(typeof(T))!.Deserialize(new StringReader(XML))!;

[Serializable, XmlRoot("REGION")]
public class RegionData
{
    [XmlAttribute("id")]
    public string id { get; init; }

    [XmlElement("NAME")]
    public string name { get; init; }

    [XmlElement("LASTUPDATE")]
    public int lastupdate { get; init; }
}

[Serializable, XmlRoot("NATION")]
public class NationData
{
    [XmlAttribute("id")]
    public string id { get; init; }

    [XmlElement("NAME")]
    public string name { get; init; }

    [XmlElement("TYPE")]
    public string type { get; init; }

    [XmlElement("FULLNAME")]
    public string fullname { get; init; }

    [XmlElement("FLAG")]
    public string flag { get; init; }
}
