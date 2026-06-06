using System.Xml.Linq;
using backend.Models;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Services;

public sealed class RoboticsNewsService(
    HttpClient httpClient,
    IMemoryCache memoryCache,
    ILogger<RoboticsNewsService> logger) : IRoboticsNewsService
{
    private static readonly string[] FeedUrls =
    [
        "https://www.therobotreport.com/feed/",
        "https://news.google.com/rss/search?q=robotics"
    ];
    private const string CacheKey = "robotics-news-feed-items";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<RoboticsNewsItem>> GetNewsAsync(int count, CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(CacheKey, out IReadOnlyList<RoboticsNewsItem>? cachedItems))
        {
            return (cachedItems ?? []).Take(count).ToArray();
        }

        try
        {
            var fetchedItems = await FetchFeedItemsAsync(cancellationToken);
            memoryCache.Set(
                CacheKey,
                fetchedItems,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration
                });

            return fetchedItems.Take(count).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch robotics news feed.");
            return [];
        }
    }

    private async Task<IReadOnlyList<RoboticsNewsItem>> FetchFeedItemsAsync(CancellationToken cancellationToken)
    {
        foreach (var feedUrl in FeedUrls)
        {
            try
            {
                await using var stream = await httpClient.GetStreamAsync(feedUrl, cancellationToken);
                var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
                var items = ParseItems(document).ToArray();
                if (items.Length > 0)
                {
                    return items;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch feed from {FeedUrl}", feedUrl);
            }
        }

        return [];
    }

    private static IEnumerable<RoboticsNewsItem> ParseItems(XDocument document)
    {
        var rootName = document.Root?.Name.LocalName;
        if (rootName == "feed")
        {
            return ParseAtomItems(document);
        }

        return ParseRssItems(document);
    }

    private static IEnumerable<RoboticsNewsItem> ParseRssItems(XDocument document)
    {
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "item")
            .Select(item =>
            {
                var title = (item.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? string.Empty).Trim();
                var url = (item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Value ?? string.Empty).Trim();
                var publishedValue = (item.Elements().FirstOrDefault(e => e.Name.LocalName == "pubDate")?.Value
                                      ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "published")?.Value
                                      ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "updated")?.Value
                                      ?? string.Empty).Trim();
                var summary = (item.Elements().FirstOrDefault(e => e.Name.LocalName == "description")?.Value
                               ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "summary")?.Value
                               ?? string.Empty).Trim();

                return CreateItem(title, url, publishedValue, summary);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Url));
    }

    private static IEnumerable<RoboticsNewsItem> ParseAtomItems(XDocument document)
    {
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "entry")
            .Select(entry =>
            {
                var title = (entry.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? string.Empty).Trim();
                var url = entry
                    .Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "link" && !string.IsNullOrWhiteSpace(e.Attribute("href")?.Value))
                    ?.Attribute("href")
                    ?.Value
                    ?.Trim()
                    ?? string.Empty;
                var publishedValue = (entry.Elements().FirstOrDefault(e => e.Name.LocalName == "published")?.Value
                                      ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "updated")?.Value
                                      ?? string.Empty).Trim();
                var summary = (entry.Elements().FirstOrDefault(e => e.Name.LocalName == "summary")?.Value
                               ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "content")?.Value
                               ?? string.Empty).Trim();

                return CreateItem(title, url, publishedValue, summary);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Url));
    }

    private static RoboticsNewsItem CreateItem(string title, string url, string publishedValue, string summary)
    {
        var parsedDate = DateTimeOffset.TryParse(publishedValue, out var published)
            ? published.ToUniversalTime()
            : DateTimeOffset.UnixEpoch;

        return new RoboticsNewsItem(
            title,
            url,
            parsedDate.ToString("O"),
            summary);
    }
}
