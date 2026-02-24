using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace PingLine.Notification.Notifications;

internal class PLNTumblr : IPingLineNotifier
{
    public const string Name = "tumblr";
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.Magenta;

    private string blogName = null!;
    private string rssUrl = null!;
    private string lastPostId = "f";

    private readonly HttpClient client = new();

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
            break;
        }
    }

    public async Task<Notification[]> Process()
    {
        try
        {
            var xml = await client.GetStringAsync(rssUrl);
            var feed = XDocument.Parse(xml);
            var items = feed.Root?.Element("channel")?.Descendants("item");
            var notifs = new List<Notification>();

            var firstItem = items?.FirstOrDefault();
            var newestID = firstItem?.Element("guid")?.Value ?? "";

            if(lastPostId == "f" && firstItem != null)
            {
                ProcessItem(firstItem, notifs);
                lastPostId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items ?? Array.Empty<XElement>())
            {
                var guid = item.Element("guid")?.Value ?? "";

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

    public void ProcessItem(XElement item, List<Notification> notifications)
    {
        var title = item.Element("title")?.Value ?? "(untitled post)";
        var description = item.Element("description")?.Value ?? title;
        var link = item.Element("link")?.Value ?? $"https://{blogName}.tumblr.com";
        var pubDate = item.Element("pubDate")?.Value ?? null;
        DateTime time;

        if(pubDate == null || !DateTime.TryParse(pubDate, out time)) time = DateTime.Now;

        var notification = new Notification()
        {
            Message = $"@{blogName} posted \"{title}\"",
            Sender = this,
            ImageSourceURL = ExtractMainImage(description),
            GoToLink = link,
            Time = time
        };
        notifications.Add(notification);
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(Name);
        writer.Write(id);
        writer.Write(blogName);
        writer.Write(rssUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNTumblr(notifID, false);
        saved.blogName = reader.ReadString();
        saved.rssUrl = reader.ReadString();
        return saved;
    }

    public string GetName() => Name;

    public static string? ExtractMainImage(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        // Look for img tag
        var imgMatch = Regex.Match(
            description,
            @"<img[^>]+>",
            RegexOptions.IgnoreCase
        );

        if (!imgMatch.Success)
            return null;

        string imgTag = imgMatch.Value;

        // Try srcset
        var srcsetMatch = Regex.Match(
            imgTag,
            @"srcset\s*=\s*""([^""]+)""",
            RegexOptions.IgnoreCase
        );

        if (srcsetMatch.Success)
        {
            string srcset = srcsetMatch.Groups[1].Value;

            string? bestUrl = null;
            int bestWidth = -1;

            var parts = srcset.Split(',');
            foreach (var part in parts)
            {
                var m = Regex.Match(part.Trim(), @"(https:[^\s]+)\s+(\d+)w");
                if (m.Success)
                {
                    string url = m.Groups[1].Value.Trim();
                    int width = int.Parse(m.Groups[2].Value);

                    if (width > bestWidth)
                    {
                        bestWidth = width;
                        bestUrl = url;
                    }
                }
            }

            if (bestUrl != null)
                return bestUrl;
        }

        // Fall back to src
        var srcMatch = Regex.Match(
            imgTag,
            @"src\s*=\s*""([^""]+)""",
            RegexOptions.IgnoreCase
        );

        if (srcMatch.Success)
            return srcMatch.Groups[1].Value;

        return null;
    }
}