using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace PingLine.Notification.Notifications;

internal class PLNTwitter : IPingLineNotifier
{
    public const string Name = "twitter";
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.Blue;

    private string accountName = null!;
    private string rssUrl = null!;
    private string lastPostId = "f";

    private static readonly HttpClient client = new();

    public PLNTwitter(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        Console.WriteLine("Pingline does not have access to the Twitter/X api. So we need to use RSS.");
        Console.WriteLine("Write {account} where the account handle should go like @example.");
        Console.WriteLine("Examples: nitter.net/{account}/rss");

        while (true)
        {
            Console.Write("RSS provider  : ");
            var rss = Console.ReadLine();
            if(string.IsNullOrEmpty(rss)) continue;

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
                await ProcessItem(firstItem, notifs);
                lastPostId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items ?? Array.Empty<XElement>())
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
        var title = item.Element("title")?.Value ?? "(untitled post)";
        var link = item.Element("link")?.Value ?? $"https://twitter.com/{accountName}";
        var pubDate = item.Element("pubDate")?.Value ?? null;
        DateTime time;

        if(pubDate == null || !DateTime.TryParse(pubDate, out time)) time = DateTime.Now;

        var notification = new Notification()
        {
            Message = $"@{accountName} posted \"{title}\"",
            Sender = this,
            ImageSourceURL = ExtractMainImage(item),
            GoToLink = link,
            Time = time
        };
        notifications.Add(notification);
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(Name);
        writer.Write(id);
        writer.Write(accountName);
        writer.Write(rssUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNTwitter(notifID, false);
        saved.accountName = reader.ReadString();
        saved.rssUrl = reader.ReadString();
        return saved;
    }

    public string GetName() => Name;

    private static readonly Regex ImgRegex = new Regex("<img[^>]+src=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? ExtractMainImage(XElement item)
    {
        if (item == null) return null;

        var description = item.Element("description")?.Value;
        if (string.IsNullOrWhiteSpace(description)) return null;

        var match = ImgRegex.Match(description);
        if (!match.Success) return null;

        var url = match.Groups[1].Value;

        if (string.IsNullOrWhiteSpace(url)) return null;

        return url;
    }
}
