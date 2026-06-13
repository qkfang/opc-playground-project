using System.Xml;
using System.Xml.Linq;
using backend.Models;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Services;

public sealed class RoboticsNewsService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache, ILogger<RoboticsNewsService> logger)
{
    private const string CacheKey = "robotics-news-items";

    public async Task<IReadOnlyList<NewsItem>> GetNewsItemsAsync(int count, CancellationToken cancellationToken)
    {
        var ttlMinutes = configuration.GetValue<int?>("RoboticsNews:CacheMinutes") ?? 5;

        var items = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, ttlMinutes));

            var feedUrl = configuration.GetValue<string>("RoboticsNews:FeedUrl")
                ?? "https://www.therobotreport.com/feed/";

            try
            {
                using var response = await httpClient.GetAsync(feedUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });

                var document = XDocument.Load(reader, LoadOptions.None);
                return ParseItems(document);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch robotics feed from {FeedUrl}", feedUrl);
                return Array.Empty<NewsItem>();
            }
        }) ?? [];

        return items.Take(count).ToArray();
    }

    private static IReadOnlyList<NewsItem> ParseItems(XDocument document)
    {
        var rssItems = document
            .Descendants()
            .Where(x => x.Name.LocalName == "item")
            .Select(ParseRssItem);

        var atomItems = document
            .Descendants()
            .Where(x => x.Name.LocalName == "entry")
            .Select(ParseAtomEntry);

        return rssItems
            .Concat(atomItems)
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Url))
            .DistinctBy(item => item.Url)
            .OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue)
            .ToArray();
    }

    private static NewsItem ParseRssItem(XElement item)
    {
        var title = ElementValue(item, "title");
        var url = ElementValue(item, "link");
        var summary = ElementValue(item, "description");
        var published = ParseDate(ElementValue(item, "pubDate"));
        return new NewsItem(title, url, published, summary);
    }

    private static NewsItem ParseAtomEntry(XElement entry)
    {
        var title = ElementValue(entry, "title");
        var url = entry
            .Elements()
            .FirstOrDefault(x => x.Name.LocalName == "link" &&
                (x.Attribute("rel")?.Value is null or "alternate"))?
            .Attribute("href")?.Value
            ?? entry.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value
            ?? string.Empty;

        var summary = ElementValue(entry, "summary");
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = ElementValue(entry, "content");
        }

        var published = ParseDate(ElementValue(entry, "published")) ?? ParseDate(ElementValue(entry, "updated"));
        return new NewsItem(title, url, published, summary);
    }

    private static string ElementValue(XElement element, string localName)
        => element.Elements().FirstOrDefault(x => x.Name.LocalName == localName)?.Value?.Trim() ?? string.Empty;

    private static DateTimeOffset? ParseDate(string value)
        => DateTimeOffset.TryParse(value, out var published) ? published : null;
}
