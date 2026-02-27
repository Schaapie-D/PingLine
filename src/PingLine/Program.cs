using PingLine.Notification;
using System.Text;

namespace PingLine;

internal class Program
{
    static bool stopApp = false;

    static void Main(string[] args)
    {
        Console.Title = "PingLine";

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Hello! Welcome to ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("PingLine");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("!\nUse the help command to start configurating!\n");

        NotificationManager.Load();

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Saving...");
            NotificationManager.Save();
        };

        Task.Run(async () =>
        {
            while (!stopApp)
            {
                NotificationManager.ProcessNotifiers();

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        });

        while (!stopApp)
        {
            ExecuteCommand(Console.ReadLine() ?? "");
        }
    }

    static void ExecuteCommand(string input)
    {
        var res = ParseArgs(input);
        var command = res.command.ToLower();
        var args = res.args;
        bool skipRewrite = false;

        bool CheckLeng(int len)
        {
            if (args.Count >= len) return false;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Invalid args. command needs atleast {len} args");
            Console.ForegroundColor = ConsoleColor.White;
            skipRewrite = true;
            return true;
        }

        switch (command)
        {
            case "help":
                Help();
                skipRewrite = true;
                break;
            case "clear":
                Console.Clear();
                skipRewrite = true;
                break;
            case "rewrite":
                // already done
                break;
            case "save":
                NotificationManager.Save();
                break;
            case "exit":
                stopApp = true;
                break;

            case "newping":
                if(CheckLeng(2)) break;
                NotificationManager.NewPingLine(args[0].ToLower(), args[1]);
                break;
            case "delping":
                if (CheckLeng(1)) break;
                NotificationManager.DeletePingLine(args[0]);
                break;
            case "lspingid":
                foreach (var n in NotificationManager.Notifiers)
                    Console.WriteLine($"{n.GetName()}:{n.id}");
                skipRewrite = true;
                break;
            case "lspingtype":
                ListPingTypes();
                skipRewrite = true;
                break;
            case "go":
                if (args.Count == 1)
                    NotificationManager.GoToNotificationLink(args[0]);
                else
                    NotificationManager.GoToNotificationLink(null);
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Command not found");
                Console.ForegroundColor = ConsoleColor.White;
                skipRewrite = true;
                break;
        }

        if (!skipRewrite) NotificationManager.RewriteNotificationLines();
    }

    static (string command, List<string> args) ParseArgs(string input)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString());
        }

        var command = args.Count > 0 ? args[0] : string.Empty;
        if (args.Count > 0) args.RemoveAt(0);

        return (command, args);
    }

    static void Help()
    {
        Console.WriteLine("Notation: <required> | [optional]");
        Console.WriteLine();
        Console.WriteLine("GENERAL COMMANDS");
        Console.WriteLine("help                           - Show this help menu");
        Console.WriteLine("clear                          - Clear the console");
        Console.WriteLine("rewrite                        - Clear screen and redraw all notification lines");
        Console.WriteLine("save                           - Save the configuration");
        Console.WriteLine("exit                           - Close the application");
        Console.WriteLine();
        Console.WriteLine("PING MANAGEMENT");
        Console.WriteLine("newping <type> <id>            - Create a new ping");
        Console.WriteLine("  Example: newping youtube NewYoutubePing");
        Console.WriteLine("           newping time NewTimePing");
        Console.WriteLine();
        Console.WriteLine("delping <id>                   - Delete a ping");
        Console.WriteLine("  Example: delping UC123456");
        Console.WriteLine();
        Console.WriteLine("lspingid                       - List all ping IDs");
        Console.WriteLine("lspingtype                     - List all ping types");
        Console.WriteLine("go [id]                        - Open the ping link");
        Console.WriteLine("  Example: go UC123456");
        Console.WriteLine("           go");
        Console.WriteLine();
        ListPingTypes();
    }

    static void ListPingTypes()
    {
        Console.WriteLine("Available Ping Types:");
        Console.WriteLine("- Youtube  (youtube)");
        Console.WriteLine("- Twitter  (twitter)");
        Console.WriteLine("- Bluesky  (bluesky)");
        Console.WriteLine("- Tumblr   (tumblr)");
        Console.WriteLine("- RSS 1.0  (rss1)");
        Console.WriteLine("- RSS 2.0  (rss2)");
        Console.WriteLine("- Atom 1.0 (atom1)");
        Console.WriteLine("- Time     (time)");
        Console.WriteLine("- Timer    (timer)");
    }
}