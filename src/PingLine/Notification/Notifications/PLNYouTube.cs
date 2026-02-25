using System.Xml.Linq;

namespace PingLine.Notification.Notifications;

internal class PLNYoutube : IPingLineNotifier
{
    public const string Name = "youtube";
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.Red;

    private string channelId = null!;
    private string? channelName;
    private string rssUrl = null!;
    private string lastVideoId = "f";

    private readonly HttpClient client = new();

    readonly XNamespace atom = "http://www.w3.org/2005/Atom";
    readonly XNamespace yt = "http://www.youtube.com/xml/schemas/2015";

    public PLNYoutube(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        Console.WriteLine("Channel ids look like UCcbrIFo2wZPvXPxJ1BCS5lQ");

        while (true)
        {
            Console.Write("Channel id: ");
            var cID = Console.ReadLine();
            if(string.IsNullOrEmpty(cID)) continue;

            rssUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={cID}";

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

            channelId = cID;

            GetChannelName();

            break;
        }
    }

    public async Task<Notification[]> Process()
    {
        try
        {
            var xml = await client.GetStringAsync(rssUrl);
            var feed = XDocument.Parse(xml);
            var items = feed.Root?.Descendants(atom + "entry")
                .OrderByDescending(item =>
                {
                    var pubDateStr = item.Element(atom + "published")?.Value;
                    return DateTime.TryParse(pubDateStr, out var dt) ? dt : DateTime.MinValue;
                })
                .ToList();
            var notifs = new List<Notification>();

            var firstItem = items?.FirstOrDefault();
            var newestID = firstItem?.Element(yt + "videoId")?.Value ?? "";

            if(lastVideoId == "f" && firstItem != null)
            {
                ProcessItem(firstItem, notifs);
                lastVideoId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items ?? new())
            {
                var guid = item.Element(yt + "videoId")?.Value ?? "";

                if (guid == lastVideoId)
                    break;

                ProcessItem(item, notifs);
            }

            lastVideoId = newestID;

            return notifs.ToArray();
        }
        catch
        {
        }

        return Array.Empty<Notification>();
    }

    private void ProcessItem(XElement item, List<Notification> notifications)
    {
        var vidID = item.Element(yt + "videoId")?.Value ?? "";
        var title = item.Element(atom + "title")?.Value ?? "(untitled)";
        var link = item.Element(atom + "link")?.Attribute("href")?.Value ?? $"https://youtube.com/channel/{channelId}";
        var pubDate = item.Element(atom + "published")?.Value;
        DateTime time;

        if(pubDate == null || !DateTime.TryParse(pubDate, out time)) time = DateTime.Now;

        var notification = new Notification()
        {
            Message = $"{channelName} uploaded \"{title}\"",
            Sender = this,
            ImageSourceURL = $"https://i.ytimg.com/vi/{vidID}/mqdefault.jpg",
            ImageHeight = 20,
            GoToLink = link,
            Time = time
        };
        notifications.Add(notification);
    }

    public void GetChannelName()
    {
        string xml = client.GetStringAsync(rssUrl).Result;
        var feed = XDocument.Parse(xml);

        channelName = feed.Root?
            .Element(atom + "author")?
            .Element(atom + "name")?.Value ?? "Unknown";
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(Name);
        writer.Write(id);
        writer.Write(channelId);
        writer.Write(rssUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNYoutube(notifID, false);
        saved.channelId = reader.ReadString();
        saved.rssUrl = reader.ReadString();
        saved.GetChannelName();
        return saved;
    }

    public string GetName() => Name;
}