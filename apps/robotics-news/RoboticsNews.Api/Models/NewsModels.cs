namespace RoboticsNews.Api.Models;

public record NewsSource(string Id, string Name, string SiteUrl, string FeedUrl);

public record NewsItem(
    string Id,
    string Title,
    string Link,
    string Summary,
    DateTimeOffset PublishedAtUtc,
    string SourceId,
    string? Author,
    string[] Tags,
    string? ImageUrl);

public record NewsResponse(DateTimeOffset GeneratedAtUtc, IReadOnlyList<NewsSource> Sources, IReadOnlyList<NewsItem> Items);
