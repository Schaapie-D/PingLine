using System.Xml.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using PingLine.Parsers;

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
    private RSS2Parser parser = null!;

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
            ImageSourceURL = await ExtractMainImage(item.GUID),
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
        var saved = new PLNBluesky(notifID, false);
        saved.accountName = reader.ReadString();
        saved.rssUrl = reader.ReadString();
        saved.parser = new RSS2Parser(saved.rssUrl);
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