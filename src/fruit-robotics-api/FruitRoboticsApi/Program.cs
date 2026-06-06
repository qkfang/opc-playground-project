using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true); // simple for demo; tighten in prod
    });
});

var app = builder.Build();

app.UseCors();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/news/robotics", (int? count) =>
{
    var take = Math.Clamp(count ?? 10, 1, 50);
    var now = DateTimeOffset.UtcNow;

    var all = new List<NewsItem>
    {
        new("Soft gripper breakthrough improves delicate fruit handling", "https://example.com/robotics/gripper-fruit", "Mock Robotics Daily", now.AddMinutes(-12)),
        new("Warehouse robots get safer with new perception stack", "https://example.com/robotics/perception-safety", "Mock Robotics Weekly", now.AddHours(-2)),
        new("Humanoid demo focuses on balance and energy efficiency", "https://example.com/robotics/humanoid-balance", "Mock RoboTimes", now.AddHours(-6)),
        new("ROS 2 tooling update streamlines deployment", "https://example.com/robotics/ros2-tooling", "Mock ROS Gazette", now.AddDays(-1)),
        new("Autonomous orchard rover maps rows with sub-meter accuracy", "https://example.com/robotics/orchard-rover", "Mock AgriBot News", now.AddDays(-2)),
        new("Tactile sensors: from lab prototypes to production", "https://example.com/robotics/tactile-sensors", "Mock Sensors Today", now.AddDays(-3)),
        new("Edge AI for mobile robots reduces latency", "https://example.com/robotics/edge-ai", "Mock Edge Review", now.AddDays(-4)),
        new("SLAM improvements for dynamic environments", "https://example.com/robotics/slam-dynamic", "Mock SLAM Notes", now.AddDays(-5)),
        new("Robot learning: imitation beats reward shaping (again)", "https://example.com/robotics/imitation-learning", "Mock ML Robotics", now.AddDays(-6)),
        new("Underwater drone navigates currents with new controller", "https://example.com/robotics/underwater-drone", "Mock Marine Robotics", now.AddDays(-7))
    };

    return Results.Ok(all.Take(take));
});

app.Run();

record NewsItem(string Title, string Url, string Source, DateTimeOffset PublishedAt);
