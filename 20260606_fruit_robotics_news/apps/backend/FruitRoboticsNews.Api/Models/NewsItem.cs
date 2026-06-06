namespace FruitRoboticsNews.Api.Models;

public record NewsItem(
    string Title,
    string Url,
    string? Source,
    DateTimeOffset? Published,
    string? Summary
);

public record NewsResponse(IReadOnlyList<NewsItem> Items);
