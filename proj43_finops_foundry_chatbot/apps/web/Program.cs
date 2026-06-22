using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Proj43.FinOps.Web.Models;
using Proj43.FinOps.Web.Services;
using Proj43.FinOps.Web.Services.Foundry;

var builder = WebApplication.CreateBuilder(args);

// ----- Options -----
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.Configure<FabricOptions>(builder.Configuration.GetSection(FabricOptions.SectionName));
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection(McpOptions.SectionName));
builder.Services.Configure<ChatOptions>(builder.Configuration.GetSection(ChatOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

// Concrete singletons for the engines/agents (they take the raw option objects).
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FoundryOptions>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FabricOptions>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<McpOptions>>().Value);

// ----- Telemetry (Application Insights when configured) -----
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// ----- Core services -----
builder.Services.AddSingleton<FinOpsDataset>(_ => new FinOpsDataset());
builder.Services.AddSingleton<FinOpsAnalytics>();
builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddSingleton<OfflineFinOpsAgent>();
builder.Services.AddSingleton<FoundryFinOpsAgent>();

// Engine selection: live Foundry agent when configured, else deterministic offline agent.
// FoundryFinOpsAgent already falls back to offline internally on any runtime failure.
builder.Services.AddSingleton<IFinOpsAgent>(sp =>
{
    var foundry = sp.GetRequiredService<FoundryOptions>();
    return foundry.IsConfigured
        ? sp.GetRequiredService<FoundryFinOpsAgent>()
        : sp.GetRequiredService<OfflineFinOpsAgent>();
});

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapOpenApi();

var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// ----- Health -----
app.MapGet("/api/health", (IFinOpsAgent agent, FoundryOptions foundry, FabricOptions fabric, McpOptions mcp, FinOpsAnalytics analytics) =>
    Results.Json(new
    {
        status = "ok",
        engine = agent.Name,
        foundryConfigured = foundry.IsConfigured,
        fabricConfigured = fabric.IsConfigured,
        mcpConfigured = mcp.IsConfigured,
        currency = analytics.Currency,
        dataThrough = analytics.EndDate.ToString("yyyy-MM-dd"),
    }, jsonOpts))
   .WithName("Health").WithSummary("Liveness + engine/config status.");

// ----- Suggested prompts (UI chips) -----
app.MapGet("/api/suggestions", () => Results.Json(AgentPersona.Suggestions, jsonOpts))
   .WithName("Suggestions").WithSummary("Starter FinOps prompts.");

// ----- Chat (non-streaming) -----
app.MapPost("/api/chat", async (ChatRequest req, IFinOpsAgent agent, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { error = "message is required" });
    var resp = await agent.ReplyAsync(req.ConversationId ?? "", req.Message, ct);
    return Results.Json(resp, jsonOpts);
}).WithName("Chat").WithSummary("Ask the FinOps assistant (full reply).");

// ----- Chat (streaming, Server-Sent Events) -----
app.MapPost("/api/chat/stream", async (HttpContext http, ChatRequest req, IFinOpsAgent agent, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        await http.Response.WriteAsync("message is required", ct);
        return;
    }

    http.Response.Headers.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers["X-Accel-Buffering"] = "no";

    async Task SendAsync(ChatStreamEvent ev)
    {
        var payload = JsonSerializer.Serialize(ev, jsonOpts);
        await http.Response.WriteAsync($"event: {ev.Type}\ndata: {payload}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    try
    {
        await foreach (var ev in agent.StreamAsync(req.ConversationId ?? "", req.Message, ct))
            await SendAsync(ev);
    }
    catch (OperationCanceledException) { /* client navigated away */ }
    catch (Exception ex)
    {
        await SendAsync(new ChatStreamEvent { Type = "error", Data = ex.Message });
    }
}).WithName("ChatStream").WithSummary("Ask the FinOps assistant (SSE token stream).");

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
