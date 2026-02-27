using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace PingLine.Parsers;

public class RSS2Parser
{
    public readonly string URL;

    public string ChannelTitle { get; private set; } = null!;
    public string ChannelLink { get; private set; } = null!;
    public string ChannelDescription { get; private set; } = null!;
    public string? ChannelImageURL { get; private set; }

    private static readonly HttpClient client;

    private static readonly XNamespace media = "http://search.yahoo.com/mrss/";

    static RSS2Parser()
    {
        client = new HttpClient();

        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/xml");

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"PingLine/{version} (+https://github.com/Schaapie-D2/PingLine)");
    }

    public RSS2Parser(string url)
    {
        URL = url;
    }

    public async Task<RSS2Entry[]> GetAndParseFeed()
    {
        return ParseFeed(await GetFeedXml());
    }

    public RSS2Entry[] ParseFeed(XDocument rssFeed)
    {
        var root = rssFeed.Root ?? throw new NullReferenceException("Invalid xml.");
        var channel = GetXElement(root, "channel") ?? throw new InvalidDataException("Invalid RSS 2.0 xml structure. Missing channel.");
        ChannelTitle = GetXElement(channel, "title")?.Value ?? throw new InvalidDataException("Invalid RSS 2.0 xml structure. Channel does not have a title.");
        ChannelLink = GetXElement(channel, "link")?.Value ?? throw new InvalidDataException("Invalid RSS 2.0 xml structure. Channel does not have a link.");
        ChannelDescription = GetXElement(channel, "description")?.Value ?? throw new InvalidDataException("Invalid RSS 2.0 xml structure. Channel does not have a description.");
        var ChannelImage = GetXElement(channel, "image");
        ChannelImageURL = GetXElement(ChannelImage, "url")?.Value;

        return GetXElements(channel, "item")
            .Select(ParseItem)
            .OrderByDescending(e => e.PubDate)
            .ToArray();
    }

    private RSS2Entry ParseItem(XElement item)
    {
        return new RSS2Entry
        {
            GUID = GetXElement(item, "guid")?.Value
                ?? GetXElement(item, "link")?.Value
                ?? throw new InvalidDataException("Entry missing guid/link"),
            Title = GetXElement(item, "title")?.Value ?? throw new InvalidDataException("Entry missing title"),
            Link = GetXElement(item, "link")?.Value ?? throw new InvalidDataException("Entry missing link"),
            Description = GetXElement(item, "description")?.Value,
            PubDate = DateTime.TryParse(GetXElement(item, "pubDate")?.Value, out var dt) ? dt : DateTime.MinValue,
            ImageURL = item.Element(media + "content")?.Attribute("url")?.Value
                ?? item.Element(media + "thumbnail")?.Attribute("url")?.Value
                ?? GetXElement(item, "enclosure")?.Attribute("url")?.Value,
            Raw = item
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

public sealed class RSS2Entry
{
    public string GUID = null!;
    public string Title = null!;
    public string Link = null!;
    public string? Description;
    public DateTime PubDate;
    public string? ImageURL;
    public XElement Raw = null!;
}