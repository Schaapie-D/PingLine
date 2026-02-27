using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PingLine.Parsers;

namespace PingLine.Notification.Notifications;

internal class PLNAtom1 : IPingLineNotifier
{
    public string Name => parser.FeedTitle;
    public const string TypeName = "atom1";
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.White;

    private string atomUrl = null!;
    private string lastPostId = "f";

    private Atom1Parser parser = null!;

    public PLNAtom1(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        var client = new HttpClient();

        while (true)
        {
            Console.Write("Atom url: ");
            var url = Console.ReadLine();
            if(string.IsNullOrEmpty(url)) continue;

            atomUrl = url;

            try
            {
                var response = client.GetAsync(atomUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to data from {atomUrl}. Please check for typos.");
                    Console.ForegroundColor = ConsoleColor.White;
                    continue;
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to data from {atomUrl}. Please check for typos.");
                Console.ForegroundColor = ConsoleColor.White;
                continue;
            }

            parser = new Atom1Parser(atomUrl);

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
            var newestID = firstItem.ID;

            if(lastPostId == "f" && firstItem != null)
            {
                ProcessItem(firstItem, notifs);
                lastPostId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items!)
            {
                var guid = item.ID;

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

    public void ProcessItem(Atom1Entry item, List<Notification> notifications)
    {
        var notification = new Notification()
        {
            Message = item.Title,
            Sender = this,
            ImageSourceURL = item.ImageURL ?? ExtractMainImage(item.Content ?? ""),
            GoToLink = item.Link,
            Time = item.Published
        };
        notifications.Add(notification);
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(TypeName);
        writer.Write(id);
        writer.Write(atomUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNAtom1(notifID, false);
        saved.atomUrl = reader.ReadString();
        saved.parser = new Atom1Parser(saved.atomUrl);
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