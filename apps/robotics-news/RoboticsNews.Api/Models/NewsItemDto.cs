namespace RoboticsNews.Api.Models;

public sealed record NewsItemDto(
    string Title,
    string Url,
    string Source,
    DateTimeOffset PublishedAt
);
