using Spectre.Console;

/*
Copyright 2022 Vleerian R.

MIT LICENSE

Permission is hereby granted, free of charge,
to any person obtaining a copy of this software and
associated documentation files (the “Software”),
to deal in the Software without restriction, including
without limitation the rights to use, copy, modify,
merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to
whom the Software is furnished to do so, subject
to the following conditions:

The above copyright notice and this permission notice
shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/

namespace FatKATT
{
    public class LogLevel
    {
        public string LevelName  { get; init; }
        public string ShortName  { get; init; }
        public string Color      { get; init; }
        public int    Threshhold { get; init; }

        private LogLevel(string levelname, string shortname, string color, int threshhold )
        {
            LevelName = levelname; 
            ShortName = shortname;
            Color = color;
            Threshhold = threshhold;
        }

        public static LogLevel Fatal        = new ("Fatal",      "FTL", "red", 10000);
        public static LogLevel Error        = new ("Error",      "ERR", "red", 9000);
        public static LogLevel Warning      = new ("Warning",    "WRN", "yellow", 8000);
        public static LogLevel Info         = new ("Info",       "INF", "cyan", 5000);
        public static LogLevel Request      = new ("Request",    "REQ", "magenta", 4000);
        public static LogLevel Processing   = new ("Processing", "PRC", "green", 3000);
        public static LogLevel Done         = new ("Done",       "FIN", "green", 3000);
        public static LogLevel Sleeping     = new ("Sleeping",   "SLP", "blue", 1000);

    }

    public static class Logger
    {
        private static readonly string AssemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!;
        public static void Log(string message, LogLevel type, Exception? e = null)
        {
            AnsiConsole.Write($"{AssemblyName} [");
            AnsiConsole.Markup($"[{type.Color}]{type.ShortName}[/]");
            AnsiConsole.Write($"] - {message}\n");

            if(e != null)
            {
                AnsiConsole.MarkupLine($"[red]{e.GetType().ToString()}[/]");
                AnsiConsole.WriteLine(e.ToString());
            }
        }

        public static void Fatal(string message) => Log(message, LogLevel.Fatal);
        public static void Fatal(string message, Exception e) => Log(message, LogLevel.Fatal, e);

        public static void Error(string message) => Log(message, LogLevel.Error);
        public static void Error(string message, Exception e) => Log(message, LogLevel.Error, e);

        public static void Warning(string message) => Log(message, LogLevel.Warning);
        public static void Warning(string message, Exception e) => Log(message, LogLevel.Warning, e);

        public static void Info(string message) => Log(message, LogLevel.Info);
        public static void Info(string message, Exception e) => Log(message, LogLevel.Info, e);

        public static void Request(string message) => Log(message, LogLevel.Request);
        public static void Request(string message, Exception e) => Log(message, LogLevel.Request, e);

        public static void Processing(string message) => Log(message, LogLevel.Processing);
        public static void Processing(string message, Exception e) => Log(message, LogLevel.Processing, e);

        public static void Done(string message) => Log(message, LogLevel.Done);
        public static void Done(string message, Exception e) => Log(message, LogLevel.Done, e);

        public static void Sleeping(string message) => Log(message, LogLevel.Sleeping);
        public static void Sleeping(string message, Exception e) => Log(message, LogLevel.Sleeping, e);
    }
}