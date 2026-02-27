using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PingLine.Parsers;

namespace PingLine.Notification.Notifications;

internal class PLNRSS2 : IPingLineNotifier
{
    public string Name => parser.ChannelTitle;
    public const string TypeName = "rss2";
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.White;

    private string rssUrl = null!;
    private string lastPostId = "f";

    private RSS2Parser parser = null!;

    public PLNRSS2(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        var client = new HttpClient();

        while (true)
        {
            Console.Write("RSS url: ");
            var url = Console.ReadLine();
            if(string.IsNullOrEmpty(url)) continue;

            rssUrl = url;

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
                var guid = item.GUID;

                if (guid == lastPostId)
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
            Message = item.Title,
            Sender = this,
            ImageSourceURL = item.ImageURL ?? ExtractMainImage(item.Description ?? ""),
            GoToLink = item.Link,
            Time = item.PubDate
        };
        notifications.Add(notification);
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(TypeName);
        writer.Write(id);
        writer.Write(rssUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNRSS2(notifID, false);
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
