using System.Xml.Linq;
using System.Text.RegularExpressions;
using PingLine.Parsers;

namespace PingLine.Notification.Notifications;

internal class PLNTumblr : IPingLineNotifier
{
    public const string Name = "tumblr";
    public const string TypeName = Name;
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.Magenta;

    private string blogName = null!;
    private string rssUrl = null!;
    private string lastPostId = "f";

    private readonly HttpClient client = new();
    private RSS2Parser parser = null!;

    public PLNTumblr(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        while (true)
        {
            Console.Write("Blog handle: ");
            var handle = Console.ReadLine();
            if(string.IsNullOrEmpty(handle)) continue;

            handle = handle.Replace("@", "");
            rssUrl = $"https://{handle}.tumblr.com/rss";

            try
            {
                var response = client.GetAsync(rssUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to data from {rssUrl}. Please check for typos.");
                    Console.ForegroundColor = ConsoleColor.White;
                    continue;
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to data from {rssUrl}. Please check for typos.");
                Console.ForegroundColor = ConsoleColor.White;
                continue;
            }

            blogName = handle;
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
                ProcessItem(firstItem, notifs);
                lastPostId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items!)
            {
                if (item.GUID == lastPostId)
                    break;

                ProcessItem(item, notifs);
            }

            lastPostId = newestID;

            return notifs.ToArray();
        }
        catch
        {
        }

        return Array.Empty<Notification>();
    }

    public void ProcessItem(RSS2Entry item, List<Notification> notifications)
    {
        var notification = new Notification()
        {
            Message = $"@{blogName} posted \"{item.Title}\"",
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
        writer.Write(blogName);
        writer.Write(rssUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNTumblr(notifID, false);
        saved.blogName = reader.ReadString();
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