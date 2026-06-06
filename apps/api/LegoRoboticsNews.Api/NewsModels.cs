namespace LegoRoboticsNews.Api;

public record NewsItem(
    string Id,
    string Title,
    string Url,
    string Source,
    DateTimeOffset PublishedAt,
    string? Summary);

public record NewsResponse(
    IReadOnlyList<NewsItem> Items,
    DateTimeOffset GeneratedAt);
