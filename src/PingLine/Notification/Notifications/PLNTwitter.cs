using System.Xml.Linq;
using System.Text.RegularExpressions;
using PingLine.Parsers;

namespace PingLine.Notification.Notifications;

internal class PLNTwitter : IPingLineNotifier
{
    public const string Name = "twitter";
    public const string TypeName = Name;
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.Blue;

    private string accountName = null!;
    private string rssUrl = null!;
    private string lastPostId = "f";

    private static readonly HttpClient client = new();
    private RSS2Parser parser = null!;

    public PLNTwitter(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        Console.WriteLine("Pingline does not have access to the Twitter/X api. So we need to use RSS.");
        Console.WriteLine("Write {account} where the account handle should go like @example.");
        Console.WriteLine("Examples: https://nitter.net/{account}/rss");

        while (true)
        {
            Console.Write("RSS provider  : ");
            var rss = Console.ReadLine();
            if(string.IsNullOrEmpty(rss)) continue;

            if (!rss.StartsWith("https://") && !rss.StartsWith("http://"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please start the url with https:// or http://.");
                Console.ForegroundColor = ConsoleColor.White;
                continue;
            }
            if (!rss.Contains("{account}"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please write {account} where the account handle should go.");
                Console.ForegroundColor = ConsoleColor.White;
                continue;
            }

            string? handle;
            while (true)
            {
                Console.Write("Account handle: ");
                handle = Console.ReadLine();
                if(string.IsNullOrEmpty(handle)) continue;
                break;
            }

            handle = handle.Replace("@", "");
            rss = rss.Replace("{account}", handle);

            try
            {
                var response = client.GetAsync(rss).Result;
                if (!response.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to data from {rss}. Please use a different provider or check for typos.");
                    Console.ForegroundColor = ConsoleColor.White;
                    continue;
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to data from {rss}. Please use a different provider or check for typos.");
                Console.ForegroundColor = ConsoleColor.White;
                continue;
            }

            rssUrl = rss;
            accountName = handle;
            parser = new RSS2Parser(rssUrl);
            break;
        }
    }

    public async Task<Notification[]> Process()
    {
        try
        {
            var items = await parser.GetAndParseFeed();
            var notifs = new List<Notification>();

            var firstItem = items?.FirstOrDefault();
            if(firstItem == null) return Array.Empty<Notification>();
            var newestID = firstItem.GUID;

            if(lastPostId == "f" && firstItem != null)
            {
                await ProcessItem(firstItem, notifs);
                lastPostId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items!)
            {
                if (item.GUID == lastPostId)
                    break;

                await ProcessItem(item, notifs);
            }

            lastPostId = newestID;

            return notifs.ToArray();
        }
        catch
        {
        }

        return Array.Empty<Notification>();
    }

    public async Task ProcessItem(RSS2Entry item, List<Notification> notifications)
    {
        var notification = new Notification()
        {
            Message = $"@{accountName} posted \"{item.Title}\"",
            Sender = this,
            ImageSourceURL = ExtractMainImage(item.Description ?? ""),
            GoToLink = item.Link,
            Time = item.PubDate
        };
        notifications.Add(notification);
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(TypeName);
        writer.Write(id);
        writer.Write(accountName);
        writer.Write(rssUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNTwitter(notifID, false);
        saved.accountName = reader.ReadString();
        saved.rssUrl = reader.ReadString();
        saved.parser = new RSS2Parser(saved.rssUrl);
        return saved;
    }

    public string GetName() => Name;
    public string GetTypeName() => TypeName;

    private static readonly Regex ImgRegex = new Regex("<img[^>]+src=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static string? ExtractMainImage(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var match = ImgRegex.Match(description);
        if (!match.Success) return null;

        var url = match.Groups[1].Value;

        if (string.IsNullOrWhiteSpace(url)) return null;

        return url;
    }
}
