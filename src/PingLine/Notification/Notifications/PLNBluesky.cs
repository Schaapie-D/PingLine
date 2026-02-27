using System.Xml.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PingLine.Notification.Notifications;

internal class PLNBluesky : IPingLineNotifier
{
    public const string Name = "bluesky";
    public const string TypeName = Name;
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.Cyan;

    private string accountName = null!;
    private string rssUrl = null!;
    private string lastPostId = "f";

    private static readonly HttpClient client = new();

    public PLNBluesky(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        while (true)
        {
            Console.Write("Account handle: ");
            var handle = Console.ReadLine();
            if(string.IsNullOrEmpty(handle)) continue;

            handle = handle.Replace("@", "");
            rssUrl = $"https://bsky.app/profile/{handle}/rss";

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

            accountName = handle;
            break;
        }
    }

    public async Task<Notification[]> Process()
    {
        try
        {
            var xml = await client.GetStringAsync(rssUrl);
            var feed = XDocument.Parse(xml);
            var items = feed.Root?.Element("channel")?
                .Descendants("item")
                .OrderByDescending(item =>
                {
                    var pubDateStr = item.Element("pubDate")?.Value;
                    return DateTime.TryParse(pubDateStr, out var dt) ? dt : DateTime.MinValue;
                })
                .ToList();
            var notifs = new List<Notification>();

            var firstItem = items?.FirstOrDefault();
            var newestID = firstItem?.Element("guid")?.Value ?? "";

            if(lastPostId == "f" && firstItem != null)
            {
                await ProcessItem(firstItem, notifs);
                lastPostId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items ?? new())
            {
                var guid = item.Element("guid")?.Value ?? "";

                if (guid == lastPostId)
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

    public async Task ProcessItem(XElement item, List<Notification> notifications)
    {
        var guid = item.Element("guid")?.Value ?? "";
        var title = item.Element("description")?.Value ?? "(untitled post)";
        var link = item.Element("link")?.Value ?? $"https://bsky.app/profile/{accountName}";
        var pubDate = item.Element("pubDate")?.Value ?? null;
        DateTime time;

        if(pubDate == null || !DateTime.TryParse(pubDate, out time)) time = DateTime.Now;

        var notification = new Notification()
        {
            Message = $"@{accountName} posted \"{title}\"",
            Sender = this,
            ImageSourceURL = await ExtractMainImage(guid),
            GoToLink = link,
            Time = time
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
        var saved = new PLNBluesky(notifID, false);
        saved.accountName = reader.ReadString();
        saved.rssUrl = reader.ReadString();
        return saved;
    }

    public string GetName() => Name;
    public string GetTypeName() => TypeName;

    public async static Task<string?> ExtractMainImage(string guid)
    {
        var url = $"https://public.api.bsky.app/xrpc/app.bsky.feed.getPostThread?uri={UrlEncoder.Default.Encode(guid)}";

        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode) return null;

        var jsonString = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.TryGetProperty("thread", out var thread) && thread.TryGetProperty("post", out var post))
        {
            // embed.images[0].fullsize
            if (post.TryGetProperty("embed", out var embed) && embed.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
            {
                var first = images[0];

                if (first.TryGetProperty("fullsize", out var full))
                    return full.GetString();
            }

            // fallback: record.embed.images
            if (post.TryGetProperty("record", out var record) && record.TryGetProperty("embed", out var recordEmbed) && recordEmbed.TryGetProperty("images", out var recordImages) && recordImages.GetArrayLength() > 0)
            {
                var first = recordImages[0];

                if (first.TryGetProperty("fullsize", out var full))
                    return full.GetString();
            }
        }

        return null;
    }
}