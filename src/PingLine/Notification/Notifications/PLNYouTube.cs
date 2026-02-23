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
            var items = feed.Root?.Descendants(atom + "entry");
            var notifs = new List<Notification>();

            var firstItem = items?.FirstOrDefault();
            var newestID = firstItem?.Element(yt + "videoId")?.Value ?? "";

            if(lastVideoId == "f" && firstItem != null)
            {
                ProcessItem(firstItem, notifs);
                lastVideoId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items ?? Array.Empty<XElement>())
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
        var title = item.Element(atom + "title")?.Value ?? "(untitled)";
        var link = item.Element(atom + "link")?.Attribute("href")?.Value ?? $"https://youtube.com/channel/{channelId}";
        var pubDate = item.Element(atom + "published")?.Value;
        DateTime time;

        if(pubDate == null || !DateTime.TryParse(pubDate, out time)) time = DateTime.Now;

        var notification = new Notification()
        {
            Message = $"{channelName} uploaded \"{title}\"",
            Sender = this,
            ImageSourceURL = ExtractThumbImage(item),
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

    public static string? ExtractThumbImage(XElement item)
    {
        XNamespace media = "http://search.yahoo.com/mrss/";

        // Find <media:group>
        var group = item.Element(media + "group");
        if (group == null) return null;

        // Get all thumbnails
        var thumbs = group.Elements(media + "thumbnail").ToList();
        if (thumbs.Count == 0) return null;

        if (thumbs.Count == 1)
            return thumbs[0].Attribute("url")?.Value;

        XElement? best = null;
        int bestWidth = -1;

        foreach (var t in thumbs)
        {
            int width = int.TryParse(t.Attribute("width")?.Value, out var w) ? w : 0;

            if (width > bestWidth)
            {
                bestWidth = width;
                best = t;
            }
        }

        return best?.Attribute("url")?.Value;
    }
}