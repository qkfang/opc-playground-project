using backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<RoboticsNewsService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173", "https://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("SwaCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("SwaCors");

app.MapGet("/api/news/robotics", async (int? count, RoboticsNewsService newsService, CancellationToken cancellationToken) =>
{
    var cappedCount = Math.Clamp(count ?? 10, 1, 50);
    var items = await newsService.GetNewsItemsAsync(cappedCount, cancellationToken);
    return Results.Ok(items);
});

app.Run();
