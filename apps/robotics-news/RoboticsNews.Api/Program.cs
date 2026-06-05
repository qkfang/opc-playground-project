using RoboticsNews.Api.Options;
using RoboticsNews.Api.Services;

const int NewsFeedTimeoutSeconds = 15;

var builder = WebApplication.CreateBuilder(args);

var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
var allowedOrigins = corsOptions.AllowedOrigins
    .Where(static origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<NewsFeedOptions>(builder.Configuration.GetSection(NewsFeedOptions.SectionName));

builder.Services.AddHttpClient<INewsService, RssNewsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(NewsFeedTimeoutSeconds);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendOrigins", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }

            return;
        }

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendOrigins");

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
