using FruitRoboticsNews.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS — allow frontend origins (configure via CORS_ORIGINS env var, comma-separated)
var corsOrigins = builder.Configuration["CORS_ORIGINS"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// In-memory cache
builder.Services.AddMemoryCache();

// HTTP client for RSS fetching
builder.Services.AddHttpClient("rss", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FruitRoboticsNews/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// News feed service
builder.Services.AddScoped<INewsFeedService, NewsFeedService>();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.MapGet("/api/news", async (INewsFeedService newsService, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        var result = await newsService.GetNewsAsync(ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to fetch news feed");
        return Results.Problem(
            title: "Failed to fetch news",
            detail: "Unable to retrieve the news feed at this time. Please try again later.",
            statusCode: 502);
    }
})
.WithName("GetNews")
.WithOpenApi();

app.Run();
