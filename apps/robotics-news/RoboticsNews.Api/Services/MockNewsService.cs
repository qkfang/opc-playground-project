using RoboticsNews.Api.Models;

namespace RoboticsNews.Api.Services;

public sealed class MockNewsService : INewsService
{
    public Task<IReadOnlyList<NewsItemDto>> GetLatestAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        IReadOnlyList<NewsItemDto> items =
        [
            new(
                Id: "rn_0001",
                Title: "Humanoid robot learns warehouse picking with vision-only policy",
                Url: "https://example.com/robotics/humanoid-warehouse-picking",
                Source: "Mock Robotics Daily",
                PublishedAt: now.AddHours(-2),
                Summary: "A lightweight vision-only policy improves grasp success in cluttered bins.",
                Tags: ["humanoid", "computer-vision", "warehouse"]
            ),
            new(
                Id: "rn_0002",
                Title: "Autonomous drones map construction sites in real time",
                Url: "https://example.com/robotics/drones-construction-mapping",
                Source: "Mock Robotics Daily",
                PublishedAt: now.AddHours(-6),
                Summary: "On-device SLAM reduces latency and increases accuracy for progress tracking.",
                Tags: ["drones", "slam", "construction"]
            ),
            new(
                Id: "rn_0003",
                Title: "Soft gripper achieves delicate fruit handling at scale",
                Url: "https://example.com/robotics/soft-gripper-fruit",
                Source: "Mock Robotics Weekly",
                PublishedAt: now.AddDays(-1),
                Summary: "New elastomer design lowers bruising while maintaining throughput.",
                Tags: ["soft-robotics", "agriculture", "grippers"]
            )
        ];

        return Task.FromResult(items);
    }
}
