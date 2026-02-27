using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace PingLine.Parsers;

public class Atom1Parser
{
    public readonly string URL;

    public string FeedTitle { get; private set; } = null!;
    public string? FeedSubtitle { get; private set; }
    public string? FeedLink { get; private set; }
    public string AuthorName { get; private set; } = "Unknown author";
    public string? AuthorURL { get; private set; }
    public string? FeedIconURL { get; private set; }
    public string? FeedLogoURL { get; private set; }

    private static readonly HttpClient client;

    private static readonly XNamespace media = "http://search.yahoo.com/mrss/";

    static Atom1Parser()
    {
        client = new HttpClient();

        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/atom+xml, application/rss+xml, application/xml, text/xml;q=0.9, */*;q=0.8");

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"PingLine/{version} (+https://github.com/Schaapie-D2/PingLine)");
    }

    public Atom1Parser(string url)
    {
        URL = url;
    }

    public async Task<Atom1Entry[]> GetAndParseFeed()
    {
        return ParseFeed(await GetFeedXml());
    }

    public Atom1Entry[] ParseFeed(XDocument atomFeed)
    {
        var root = atomFeed.Root ?? throw new NullReferenceException("Invalid xml.");
        FeedTitle = GetXElement(root, "title")?.Value ?? throw new InvalidDataException("Invalid Atom 1.0 xml structure. Feed does not have a title.");
        FeedSubtitle = GetXElement(root, "subtitle")?.Value;
        FeedLink = GetXElements(root, "link").FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate")?.Attribute("href")?.Value;
        var author = GetXElement(root, "author");
        AuthorName = GetXElement(author, "name")?.Value ?? AuthorName;
        AuthorURL = GetXElement(author, "uri")?.Value;
        FeedIconURL = GetXElement(root, "icon")?.Value;
        FeedLogoURL = GetXElement(root, "logo")?.Value;

        return GetXElements(root, "entry")
            .Select(ParseItem)
            .OrderByDescending(e => e.Published)
            .ToArray();
    }

    private Atom1Entry ParseItem(XElement item)
    {
        var summaryElement = GetXElement(item, "summary");
        var contentElement = GetXElement(item, "content");
        string? content = null;

        if (summaryElement != null)
        {
            content = summaryElement.Value;
        }
        else if (contentElement != null)
        {
            var type = (string?)contentElement.Attribute("type") ?? "text";

            content = type == "xhtml"
                ? contentElement.Elements().FirstOrDefault()?.ToString(SaveOptions.DisableFormatting)
                : contentElement.Value;
        }

        var imageURL = GetXElements(item, "link")?.FirstOrDefault(l => l.Attribute("rel")?.Value == "enclosure" && (l.Attribute("type")?.Value ?? "").StartsWith("image/"))?.Attribute("href")?.Value
            ?? item.Descendants(media + "thumbnail")?.FirstOrDefault()?.Attribute("url")?.Value;

        return new Atom1Entry
        {
            ID = GetXElement(item, "id")?.Value ?? throw new InvalidDataException("Entry missing id"),
            Title = GetXElement(item, "title")?.Value ?? throw new InvalidDataException("Entry missing title"),
            Link = GetXElements(item, "link")?.FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate")?.Attribute("href")?.Value,
            Content = content,
            AuthorName = GetXElement(GetXElement(item, "author"), "name")?.Value ?? AuthorName,
            Published = DateTime.TryParse(GetXElement(item, "published")?.Value ?? GetXElement(item, "updated")?.Value, out var dt) ? dt : DateTime.MinValue,
            ImageURL = imageURL,
            Raw = item,
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

public sealed class Atom1Entry
{
    public string ID = null!;
    public string Title = null!;
    public string? Link;
    public string? Content;
    public string AuthorName = null!;
    public DateTime Published;
    public string? ImageURL;
    public XElement Raw = null!;
}