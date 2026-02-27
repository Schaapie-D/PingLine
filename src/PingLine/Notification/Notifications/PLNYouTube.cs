using System.Xml.Linq;
using PingLine.Parsers;

namespace PingLine.Notification.Notifications;

internal class PLNYoutube : IPingLineNotifier
{
    public const string Name = "youtube";
    public const string TypeName = Name;
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.Red;

    private string channelId = null!;
    private string atomUrl = null!;
    private string lastVideoId = "f";

    private readonly HttpClient client = new();
    private Atom1Parser parser = null!;

    private static readonly XNamespace yt = "http://www.youtube.com/xml/schemas/2015";

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

            atomUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={cID}";

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

            channelId = cID;
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

            if(lastVideoId == "f" && firstItem != null)
            {
                ProcessItem(firstItem, notifs);
                lastVideoId = newestID;
                return notifs.ToArray();
            }

            foreach (var item in items!)
            {
                if (item.ID == lastVideoId)
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

    private void ProcessItem(Atom1Entry item, List<Notification> notifications)
    {
        var vidID = item.Raw.Element(yt + "videoId")?.Value ?? "";

        var notification = new Notification()
        {
            Message = $"{item.AuthorName} uploaded \"{item.Title}\"",
            Sender = this,
            ImageSourceURL = $"https://i.ytimg.com/vi/{vidID}/mqdefault.jpg",
            ImageHeight = 20,
            GoToLink = item.Link,
            Time = item.Published
        };
        notifications.Add(notification);
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(TypeName);
        writer.Write(id);
        writer.Write(channelId);
        writer.Write(atomUrl);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNYoutube(notifID, false);
        saved.channelId = reader.ReadString();
        saved.atomUrl = reader.ReadString();
        saved.parser = new Atom1Parser(saved.atomUrl);
        return saved;
    }

    public string GetName() => Name;
    public string GetTypeName() => TypeName;
}