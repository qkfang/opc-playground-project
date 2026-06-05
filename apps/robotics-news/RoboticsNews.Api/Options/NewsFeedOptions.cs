namespace RoboticsNews.Api.Options;

public sealed class NewsFeedOptions
{
    public const string SectionName = "NewsFeeds";

    public int CacheDurationMinutes { get; init; } = 5;

    public string[] FeedUrls { get; init; } = [];
}
