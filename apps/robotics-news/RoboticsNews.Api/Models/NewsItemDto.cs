namespace RoboticsNews.Api.Models;

public sealed record NewsItemDto(
    string Id,
    string Title,
    string Url,
    string Source,
    DateTimeOffset PublishedAt,
    string? Summary,
    string[] Tags
);
