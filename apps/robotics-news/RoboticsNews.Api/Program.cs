using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Permissive CORS for MVP/local dev. Tighten to your SWA hostname in prod.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

app.MapGet("/api/news", async () =>
{
    var dataPath = Path.Combine(app.Environment.ContentRootPath, "Data", "news.mock.json");
    var json = await File.ReadAllTextAsync(dataPath);

    var articles = JsonSerializer.Deserialize<List<NewsArticle>>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    }) ?? [];

    return Results.Ok(articles);
})
.WithName("GetRoboticsNews");

app.Run();

internal sealed record NewsArticle(
    string Id,
    string Title,
    string Source,
    string Url,
    DateTimeOffset PublishedAt,
    string Summary,
    string[] Tags);
