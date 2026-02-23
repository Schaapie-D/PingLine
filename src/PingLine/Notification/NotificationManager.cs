using PingLine.Notification.Notifications;
using System.Diagnostics;

namespace PingLine.Notification;

internal static class NotificationManager
{
    public static List<IPingLineNotifier> Notifiers = new();
    public static List<(Notification notification, IPingLineNotifier notifier)> History = new();

    static readonly string SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PingLine", "pingline.cfg");

    static Dictionary<string, string> GoToLinkDict = new();
    static string? GoToLink = null;

    static DateTime currentDate = DateTime.MinValue;

    public static void NewPingLine(string notifierType, string notifierID)
    {
        IPingLineNotifier newNotifier;
        switch (notifierType)
        {
            case PLNTumblr.Name: newNotifier = new PLNTumblr(notifierID); break;
            case PLNTime.Name: newNotifier = new PLNTime(notifierID); break;
            case PLNTimer.Name: newNotifier = new PLNTimer(notifierID); break;
            case PLNYoutube.Name: newNotifier = new PLNYoutube(notifierID); break;
            case PLNBluesky.Name: newNotifier = new PLNBluesky(notifierID); break;
            case PLNTwitter.Name: newNotifier = new PLNTwitter(notifierID); break;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please provide a valid ping type");
                Console.ForegroundColor = ConsoleColor.White;
                return;
        }

        Notifiers.Add(newNotifier);
        ProcessNotifiers();
    }

    public static void DeletePingLine(string notifID)
    {
        var notif = Notifiers.FirstOrDefault(n => n.id == notifID);
        if (notif != null)
        {
            Notifiers.Remove(notif);
        }
    }

    public static async void ProcessNotifiers()
    {
        List<(Notification notification, IPingLineNotifier notifier)> entrys = new();

        foreach(var notifier in Notifiers)
        {
            foreach(var notification in await notifier.Process())
            {
                entrys.Add((notification, notifier));
            }
        }

        entrys = entrys.OrderBy(e => e.notification.Time).ToList();

        foreach(var entry in entrys)
        {
            await ProcessNotification(entry.notification, entry.notifier, true);
        }
    }

    private static async Task ProcessNotification(Notification notification, IPingLineNotifier notifier, bool addToHistory)
    {
        if(currentDate != notification.Time.Date)
        {
            Console.WriteLine($"================= {notification.Time:dd/MM/yyyy} =================");
            currentDate = notification.Time.Date;
        }

        Console.ForegroundColor = notifier.TextColor;

        string text = $"{notification.Time:HH:mm} | {notifier.GetName()} - {notification.Message}";

        if(addToHistory) History.Add((notification, notifier));

        if (notification.GoToLink != null)
        {
            GoToLink = notification.GoToLink;
            GoToLinkDict[notifier.id] = notification.GoToLink;
        }

        Console.WriteLine(text);
        if(notification.ImageSourceURL != null)
        {
            var art = await AsciiArtGenerator.GenerateFromUrl(notification.ImageSourceURL, notification.ImageHeight ?? 30);
            WriteImage(art, notifier);
        }

        Console.ForegroundColor = ConsoleColor.White;
    }

    private static void WriteImage(string[] asciiArtLines, IPingLineNotifier notifier)
    {
        Console.ForegroundColor = notifier.TextColor;

        foreach (var line in asciiArtLines)
        {
            Console.ForegroundColor = notifier.TextColor;
            Console.Write("\x1b[?7l"); // Disable line wrapping
            Console.Write("      | ");
            Console.WriteLine(line);
            Console.Write("\x1b[?7h"); // Enable line wrapping
        }

        Console.ForegroundColor = ConsoleColor.White;
    }

    public static async void RewriteNotificationLines()
    {
        Console.Clear();
        currentDate = DateTime.MinValue;
        History = History.OrderBy(n => n.notification.Time).ToList();
        foreach (var entry in History)
        {
            await ProcessNotification(entry.notification, entry.notifier, false);
        }
    }

    public static void Save()
    {
        if(!Directory.Exists(Path.GetDirectoryName(SavePath))) Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);

        using var stream = File.Open(SavePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write(Notifiers.Count);
        foreach (var n in Notifiers)
        {
            n.AppendSaveInfo(writer);
        }
    }

    public static void Load()
    {
        if (!File.Exists(SavePath)) return;

        using var stream = File.Open(SavePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        int notifierCount = reader.ReadInt32();
        for (int i = 0; i < notifierCount; i++)
        {
            string notifierName = reader.ReadString();
            string notifierID = reader.ReadString();
            switch (notifierName)
            {
                case PLNTumblr.Name: Notifiers.Add(PLNTumblr.CreateFromSave(notifierID, reader)); break;
                case PLNTime.Name: Notifiers.Add(PLNTime.CreateFromSave(notifierID, reader)); break;
                case PLNTimer.Name: Notifiers.Add(PLNTimer.CreateFromSave(notifierID, reader)); break;
                case PLNYoutube.Name: Notifiers.Add(PLNYoutube.CreateFromSave(notifierID, reader)); break;
                case PLNBluesky.Name: Notifiers.Add(PLNBluesky.CreateFromSave(notifierID, reader)); break;
                case PLNTwitter.Name: Notifiers.Add(PLNTwitter.CreateFromSave(notifierID, reader)); break;

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to load a ping from save file: Unknown ping type: {notifierName}");
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }
        }
    }

    public static void GoToNotificationLink(string? notifID)
    {
        string? link;

        if (!string.IsNullOrEmpty(notifID) && GoToLinkDict.TryGetValue(notifID, out var foundLink))
            link = foundLink;
        else
            link = GoToLink;

        if (string.IsNullOrEmpty(link)) return;

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", link);
        }
        else if (OperatingSystem.IsLinux())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = link,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
        }
    }
}

internal interface IPingLineNotifier
{
    string id { get; set; }
    ConsoleColor TextColor { get; set; }
    string GetName();
    Task<Notification[]> Process();
    void AppendSaveInfo(BinaryWriter writer);
}

internal struct Notification
{
    public string Message;
    public IPingLineNotifier Sender;
    public string? ImageSourceURL;
    public int? ImageHeight;
    public string? GoToLink;
    public DateTime Time;
}