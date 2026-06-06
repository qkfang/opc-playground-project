using System.Net;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace LegoRoboticsNews.Api;

public sealed class NewsService
{
    private readonly HttpClient _http;

    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(10);
    private readonly object _lock = new();
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;
    private List<NewsItem> _cacheItems = new();

    // Start with a small curated list; easy to extend.
    private static readonly (string Name, string Url)[] Feeds = new[]
    {
        ("IEEE Spectrum: Robotics", "https://spectrum.ieee.org/robotics/rss"),
        ("The Robot Report", "https://www.therobotreport.com/feed/"),
        ("Robotics Business Review", "https://www.roboticsbusinessreview.com/feed/"),
    };

    public NewsService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(15);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LegoRoboticsNewsBot/1.0 (+https://example.invalid)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml;q=0.9, */*;q=0.8");
    }

    public async Task<NewsResponse> GetNewsAsync(int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);

        List<NewsItem>? cached = null;
        lock (_lock)
        {
            if (DateTimeOffset.UtcNow < _cacheExpiresAt && _cacheItems.Count > 0)
            {
                cached = _cacheItems;
            }
        }

        if (cached is not null)
        {
            return new NewsResponse(cached.Take(limit).ToList(), DateTimeOffset.UtcNow);
        }

        var all = new List<NewsItem>();

        // Fetch in parallel
        var tasks = Feeds.Select(f => FetchFeedAsync(f.Name, f.Url, ct)).ToArray();
        var results = await Task.WhenAll(tasks);
        foreach (var list in results)
        {
            all.AddRange(list);
        }

        // Dedupe by URL, keep newest
        var deduped = all
            .GroupBy(i => NormalizeUrl(i.Url), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.PublishedAt).First())
            .OrderByDescending(i => i.PublishedAt)
            .Take(200)
            .ToList();

        lock (_lock)
        {
            _cacheItems = deduped;
            _cacheExpiresAt = DateTimeOffset.UtcNow.Add(_cacheTtl);
        }

        return new NewsResponse(deduped.Take(limit).ToList(), DateTimeOffset.UtcNow);
    }

    private async Task<List<NewsItem>> FetchFeedAsync(string sourceName, string feedUrl, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, feedUrl);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (resp.StatusCode == HttpStatusCode.Forbidden || resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new();
            }

            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Ignore
            });

            var feed = SyndicationFeed.Load(xmlReader);
            if (feed is null) return new();

            var items = new List<NewsItem>();
            foreach (var item in feed.Items ?? Enumerable.Empty<SyndicationItem>())
            {
                var link = item.Links?.FirstOrDefault(l => l.RelationshipType is null || l.RelationshipType == "alternate")?.Uri
                           ?? item.Links?.FirstOrDefault()?.Uri;

                if (link is null) continue;

                var published = item.PublishDate != DateTimeOffset.MinValue
                    ? item.PublishDate
                    : (item.LastUpdatedTime != DateTimeOffset.MinValue ? item.LastUpdatedTime : DateTimeOffset.UtcNow);

                var title = item.Title?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var summary = item.Summary?.Text;
                summary = string.IsNullOrWhiteSpace(summary) ? null : WebUtility.HtmlDecode(StripTags(summary)).Trim();

                var url = link.ToString();
                var id = HashToId(url);

                items.Add(new NewsItem(id, title, url, sourceName, published, summary));
            }

            return items;
        }
        catch
        {
            // swallow per-feed failures to keep endpoint resilient
            return new();
        }
    }

    private static string NormalizeUrl(string url)
    {
        return url.Trim();
    }

    private static string HashToId(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static string StripTags(string html)
    {
        var sb = new StringBuilder(html.Length);
        bool inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(ch);
        }
        return sb.ToString();
    }
}
