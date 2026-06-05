using System.Xml.Linq;
using System.Xml;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RoboticsNews.Api.Models;
using RoboticsNews.Api.Options;

namespace RoboticsNews.Api.Services;

public sealed class RssNewsService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<NewsFeedOptions> options,
    ILogger<RssNewsService> logger) : INewsService
{
    private const string CacheKey = "RssNewsService:latest";
    private const int MinCacheDurationMinutes = 1;
    private const int MaxCacheDurationMinutes = 60;

    public async Task<IReadOnlyList<NewsItemDto>> GetLatestAsync(int limit, CancellationToken cancellationToken)
    {
        var items = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            var cacheDuration = Math.Clamp(options.Value.CacheDurationMinutes, MinCacheDurationMinutes, MaxCacheDurationMinutes);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheDuration);
            return await FetchLatestAsync(cancellationToken);
        });

        return (items ?? []).Take(limit).ToArray();
    }

    private async Task<IReadOnlyList<NewsItemDto>> FetchLatestAsync(CancellationToken cancellationToken)
    {
        var feedUrls = options.Value.FeedUrls
            .Where(static url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (feedUrls.Length == 0)
        {
            throw new InvalidOperationException("No RSS feeds are configured.");
        }

        var items = new List<NewsItemDto>();

        foreach (var feedUrl in feedUrls)
        {
            try
            {
                await using var stream = await httpClient.GetStreamAsync(feedUrl, cancellationToken);
                var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
                items.AddRange(ParseFeed(document, feedUrl));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Unable to read robotics feed {FeedUrl}", feedUrl);
            }
        }

        var latestItems = items
            .Where(static item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Url))
            .DistinctBy(static item => item.Url, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static item => item.PublishedAt)
            .ToArray();

        if (latestItems.Length == 0)
        {
            throw new InvalidOperationException("No news items could be loaded from the configured RSS feeds.");
        }

        return latestItems;
    }

    private static IEnumerable<NewsItemDto> ParseFeed(XDocument document, string feedUrl)
    {
        var root = document.Root;
        if (root is null)
        {
            yield break;
        }

        foreach (var item in root.Name.LocalName switch
        {
            "rss" => ParseRssItems(root, feedUrl),
            "feed" => ParseAtomItems(root, feedUrl),
            _ => []
        })
        {
            yield return item;
        }
    }

    private static IEnumerable<NewsItemDto> ParseRssItems(XElement root, string feedUrl)
    {
        var channel = root.Elements().FirstOrDefault(static element => element.Name.LocalName == "channel");
        if (channel is null)
        {
            yield break;
        }

        var source = NormalizeWhitespace(GetElementValue(channel, "title")) ?? GetHost(feedUrl);

        foreach (var item in channel.Elements().Where(static element => element.Name.LocalName == "item"))
        {
            var title = NormalizeWhitespace(GetElementValue(item, "title"));
            var url = NormalizeWhitespace(GetElementValue(item, "link"));

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            yield return new NewsItemDto(
                Title: title,
                Url: url,
                Source: source,
                PublishedAt: GetPublishedAt(item)
            );
        }
    }

    private static IEnumerable<NewsItemDto> ParseAtomItems(XElement root, string feedUrl)
    {
        var source = NormalizeWhitespace(GetElementValue(root, "title")) ?? GetHost(feedUrl);

        foreach (var entry in root.Elements().Where(static element => element.Name.LocalName == "entry"))
        {
            var title = NormalizeWhitespace(GetElementValue(entry, "title"));
            var url = entry.Elements()
                .FirstOrDefault(static element =>
                    element.Name.LocalName == "link" &&
                    (string.Equals((string?)element.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase) ||
                     element.Attribute("rel") is null))
                ?.Attribute("href")
                ?.Value;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            yield return new NewsItemDto(
                Title: title,
                Url: url,
                Source: source,
                PublishedAt: GetPublishedAt(entry)
            );
        }
    }

    private static DateTimeOffset GetPublishedAt(XElement item)
    {
        foreach (var fieldName in new[] { "pubDate", "published", "updated", "date" })
        {
            if (DateTimeOffset.TryParse(GetElementValue(item, fieldName), out var publishedAt))
            {
                return publishedAt;
            }
        }

        return DateTimeOffset.UnixEpoch;
    }

    private static string? GetElementValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value;

    private static string GetHost(string feedUrl) =>
        Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) ? uri.Host : feedUrl;

    private static string? NormalizeWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split(default(string[]?), StringSplitOptions.RemoveEmptyEntries));
}
