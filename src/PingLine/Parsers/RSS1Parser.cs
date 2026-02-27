using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace PingLine.Parsers;

public class RSS1Parser
{
    public readonly string URL;

    public string ChannelTitle { get; private set; } = null!;
    public string ChannelLink { get; private set; } = null!;
    public string ChannelDescription { get; private set; } = null!;
    public string? ChannelImageURL { get; private set; }

    private static readonly HttpClient client;

    private static readonly XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    static RSS1Parser()
    {
        client = new HttpClient();

        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/xml");

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"PingLine/{version} (+https://github.com/Schaapie-D2/PingLine)");
    }

    public RSS1Parser(string url)
    {
        URL = url;
    }

    public async Task<RSS1Item[]> GetAndParseFeed()
    {
        return ParseFeed(await GetFeedXml());
    }

    public RSS1Item[] ParseFeed(XDocument rssFeed)
    {
        var root = rssFeed.Root ?? throw new NullReferenceException("Invalid xml.");
        var channel = GetXElement(root, "channel") ?? throw new InvalidDataException("Invalid RSS 1.0 xml structure. Missing channel.");
        ChannelTitle = GetXElement(channel, "title")?.Value ?? throw new InvalidDataException("Invalid RSS 1.0 xml structure. Channel does not have a title.");
        ChannelLink = GetXElement(channel, "link")?.Value ?? throw new InvalidDataException("Invalid RSS 1.0 xml structure. Channel does not have a link.");
        ChannelDescription = GetXElement(channel, "description")?.Value ?? throw new InvalidDataException("Invalid RSS 1.0 xml structure. Channel does not have a description.");
        var ChannelImage = GetXElement(channel, "image");
        ChannelImageURL = ChannelImage?.Attribute(rdf + "resource")?.Value ?? GetXElement(ChannelImage, "url")?.Value;

        var channelItems = GetXElement(channel, "items") ?? throw new InvalidDataException("Invalid RSS 1.0 xml structure. Channel does not have an item sequence.");
        var seq = GetXElement(channelItems, "Seq");
        var items = GetXElements(root, "item");
        var feedOrder = new List<RSS1Item>();

        if (seq != null)
        {
            var itemsSeq = GetXElements(seq, "li")
                .Select(li => li.Attribute(rdf + "resource")?.Value)
                .Where(v => v != null);

            foreach (var about in itemsSeq)
            {
                var item = items.FirstOrDefault(i => i.Attribute(rdf + "about")?.Value == about);
                if (item != null)
                    feedOrder.Add(ParseItem(item));
            }
        }
        else
        {
            feedOrder.AddRange(items.Select(ParseItem));
        }

        return feedOrder.ToArray();
    }

    private RSS1Item ParseItem(XElement item)
    {
        return new RSS1Item
        {
            ID = item.Attribute(rdf + "about")?.Value ?? "",
            Title = GetXElement(item, "title")?.Value ?? throw new InvalidDataException("Item missing title"),
            Link = GetXElement(item, "link")?.Value ?? throw new InvalidDataException("Item missing link"),
            Description = GetXElement(item, "description")?.Value,
            Raw = item.Elements().ToArray()
        };
    }

    public async Task<XDocument> GetFeedXml()
    {
        var xml = await client.GetStringAsync(URL);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };

        using var reader = XmlReader.Create(new StringReader(xml), settings);
        return XDocument.Load(reader);
    }

    private static XElement? GetXElement(XElement? parent, string localName) => parent?.Elements().FirstOrDefault(i => i.Name.LocalName == localName);
    private static XElement[] GetXElements(XElement? parent, string localName) => parent?.Elements().Where(i => i.Name.LocalName == localName).ToArray() ?? Array.Empty<XElement>();
}

public sealed class RSS1Item
{
    public string ID = null!;
    public string Title = null!;
    public string Link = null!;
    public string? Description;
    public XElement[] Raw = null!;
}