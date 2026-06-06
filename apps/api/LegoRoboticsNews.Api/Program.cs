using LegoRoboticsNews.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<NewsService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)); // simple for demo
});

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Ok(new { name = "LegoRoboticsNews.Api", status = "ok" }));

app.MapGet("/api/news", async (int? limit, NewsService news, CancellationToken ct) =>
{
    var result = await news.GetNewsAsync(limit ?? 30, ct);
    return Results.Ok(result);
});

app.Run();
