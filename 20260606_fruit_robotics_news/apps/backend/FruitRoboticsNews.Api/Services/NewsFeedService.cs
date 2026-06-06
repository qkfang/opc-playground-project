using System.ServiceModel.Syndication;
using System.Xml;
using FruitRoboticsNews.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FruitRoboticsNews.Api.Services;

public interface INewsFeedService
{
    Task<NewsResponse> GetNewsAsync(CancellationToken cancellationToken = default);
}

public class NewsFeedService : INewsFeedService
{
    private const string CacheKey = "news_feed";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NewsFeedService> _logger;

    public NewsFeedService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<NewsFeedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<NewsResponse> GetNewsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out NewsResponse? cached) && cached is not null)
        {
            return cached;
        }

        var feedUrl = _configuration["NEWS_FEED_URL"]
            ?? "https://news.google.com/rss/search?q=robotics&hl=en-US&gl=US&ceid=US:en";

        _logger.LogInformation("Fetching RSS feed from {FeedUrl}", feedUrl);

        var response = await FetchFeedAsync(feedUrl, cancellationToken);

        _cache.Set(CacheKey, response, CacheDuration);

        return response;
    }

    private async Task<NewsResponse> FetchFeedAsync(string feedUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("rss");
        using var stream = await client.GetStreamAsync(feedUrl, cancellationToken);

        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        var feed = SyndicationFeed.Load(xmlReader);

        var items = feed.Items
            .Select(item => new NewsItem(
                Title: item.Title?.Text ?? string.Empty,
                Url: item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty,
                Source: item.SourceFeed?.Title?.Text ?? feed.Title?.Text,
                Published: item.PublishDate == DateTimeOffset.MinValue
                    ? (item.LastUpdatedTime == DateTimeOffset.MinValue ? null : item.LastUpdatedTime)
                    : item.PublishDate,
                Summary: StripHtml(item.Summary?.Text)
            ))
            .Where(i => !string.IsNullOrEmpty(i.Url))
            .ToList();

        return new NewsResponse(items);
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        // Remove HTML tags and decode entities
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        result = System.Net.WebUtility.HtmlDecode(result);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s{2,}", " ").Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
