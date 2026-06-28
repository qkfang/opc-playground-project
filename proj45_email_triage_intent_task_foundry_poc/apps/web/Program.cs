using Proj45.RelayDesk.Web.Models;
using Proj45.RelayDesk.Web.Services;
using Proj45.RelayDesk.Web.Services.Foundry;
using Proj45.RelayDesk.Web.Services.Mcp;

var builder = WebApplication.CreateBuilder(args);

// ----- Configuration / options -----
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

var foundryOptions = builder.Configuration.GetSection(FoundryOptions.SectionName).Get<FoundryOptions>() ?? new FoundryOptions();
builder.Services.AddSingleton(foundryOptions);

// ----- Telemetry (Application Insights when a connection string is present) -----
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// ----- Core services -----
builder.Services.AddSingleton<MailboxWatchService>();
builder.Services.AddSingleton<ID365McpServer, MockD365McpServer>();
builder.Services.AddSingleton<HumanReviewQueue>();
builder.Services.AddSingleton<CaseStore>();
builder.Services.AddSingleton<OfflineEmailPipeline>();

// Pipeline selection: live Foundry prompt agents when configured, else deterministic offline.
// The Foundry pipeline already falls back to offline per-stage on any runtime failure.
builder.Services.AddSingleton<IEmailPipeline>(sp =>
{
    var opts = sp.GetRequiredService<FoundryOptions>();
    var offline = sp.GetRequiredService<OfflineEmailPipeline>();
    return opts.IsConfigured
        ? new FoundryEmailPipeline(opts, offline,
            sp.GetRequiredService<ID365McpServer>(),
            sp.GetRequiredService<HumanReviewQueue>(),
            sp.GetRequiredService<ILogger<FoundryEmailPipeline>>())
        : offline;
});

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapOpenApi();
app.MapRazorPages();

// ---------------------------------------------------------------- API ----

var api = app.MapGroup("/api").WithTags("RelayDesk");

api.MapGet("/health", (IEmailPipeline pipeline) => Results.Ok(new
{
    status = "healthy",
    engine = pipeline.Name,
    foundryConfigured = foundryOptions.IsConfigured,
    foundryEnabled = foundryOptions.Enabled,
    foundryMode = foundryOptions.IsConfigured ? "configured" : (foundryOptions.Enabled ? "misconfigured" : "offline"),
    modelDeployment = foundryOptions.IsConfigured ? foundryOptions.ModelDeployment : null,
    intentHumanReviewThreshold = foundryOptions.IntentHumanReviewThreshold,
    service = "Relay Desk — Inbound Email Orchestration",
    time = DateTimeOffset.UtcNow
}))
.WithName("GetHealth")
.WithDescription("Liveness/readiness probe and active engine mode (static; no Foundry network call).");

// Active Foundry readiness probe: real minimal agent round-trip (live | fallback | error | offline).
api.MapGet("/health/foundry", async (IEmailPipeline pipeline, CancellationToken ct) =>
{
    var diag = await pipeline.ProbeAsync(ct);
    var ok = diag.FoundryMode is "live" or "offline";
    return Results.Json(new
    {
        status = ok ? "ok" : "degraded",
        engine = pipeline.Name,
        foundryMode = diag.FoundryMode,
        foundryLive = diag.FoundryLive,
        foundryConfigured = diag.FoundryConfigured,
        foundryEnabled = diag.FoundryEnabled,
        endpointHost = diag.EndpointHost,
        modelDeployment = diag.ModelDeployment,
        probeMs = diag.ProbeMs,
        detail = diag.Detail,
        time = diag.CheckedUtc
    }, statusCode: ok ? 200 : 503);
})
.WithName("GetFoundryHealth")
.WithDescription("Active live-Foundry readiness probe (real agent round-trip); distinguishes live/fallback/error/offline.");

// The per-page Foundry agent instruction sets (surfaced in the UI for transparency).
api.MapGet("/agents", () => Results.Ok(AgentInstructions.All.Select(a => new
{
    a.Key, a.Page, a.Name, a.Role, a.Instructions
})))
.WithName("GetAgents")
.WithDescription("Returns the explicit Foundry agent instruction set used by each page.");

// The mock D365 MCP tool catalog (surfaced on the Task page).
api.MapGet("/mcp/tools", (ID365McpServer mcp) => Results.Ok(mcp.Catalog))
    .WithName("GetMcpTools")
    .WithDescription("Lists the mock Dynamics 365 MCP tool catalog the task agent can call.");

// The watched mailbox (mock).
api.MapGet("/inbox", (MailboxWatchService mailbox) => Results.Ok(mailbox.Inbox()))
    .WithName("GetInbox")
    .WithDescription("Returns the mock watched mailbox that triggers the orchestration pipeline.");

api.MapGet("/cases", (CaseStore store) => Results.Ok(store.List().Select(ToListItem)))
    .WithName("ListCases")
    .WithDescription("Lists processed email cases (most recent first).");

api.MapGet("/cases/{caseId}", (string caseId, CaseStore store) =>
{
    var c = store.Get(caseId);
    return c is null ? Results.NotFound(new { error = "Case not found", caseId }) : Results.Ok(c);
})
.WithName("GetCase")
.WithDescription("Returns the full processed case (extraction, triage, intent, task, outcome, trace).");

// Process a mailbox email by id through the pipeline.
api.MapPost("/cases/from-inbox/{emailId}", async (string emailId, MailboxWatchService mailbox, IEmailPipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    var email = mailbox.Get(emailId);
    if (email is null) return Results.NotFound(new { error = "Email not found", emailId });
    var c = await pipeline.RunAsync(email, ct);
    mailbox.MarkRead(emailId);
    store.Save(c);
    return Results.Ok(c);
})
.WithName("ProcessInboxEmail")
.WithDescription("Runs the extraction → triage → intent → task(D365 MCP) → outcome pipeline for a mailbox email.");

// Process an ad-hoc email posted from the UI.
api.MapPost("/cases", async (IncomingEmail email, IEmailPipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(email.From) && string.IsNullOrWhiteSpace(email.Body))
        return Results.BadRequest(new { error = "Provide at least 'from' or 'body'." });
    if (string.IsNullOrWhiteSpace(email.Id)) email.Id = Guid.NewGuid().ToString("N")[..10];
    email.ReceivedUtc = DateTimeOffset.UtcNow;
    var c = await pipeline.RunAsync(email, ct);
    store.Save(c);
    return Results.Ok(c);
})
.WithName("ProcessAdhocEmail")
.WithDescription("Runs the pipeline against an ad-hoc email payload.")
.DisableAntiforgery();

// Process the whole demo inbox in one call (great for demos/CI/smoke).
api.MapPost("/cases/run-demo", async (MailboxWatchService mailbox, IEmailPipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    var results = new List<object>();
    foreach (var email in mailbox.Inbox())
    {
        var c = await pipeline.RunAsync(email, ct);
        mailbox.MarkRead(email.Id);
        store.Save(c);
        results.Add(ToListItem(c));
    }
    return Results.Ok(new { processed = results.Count, engine = pipeline.Name, cases = results });
})
.WithName("RunDemoInbox")
.WithDescription("Processes every email in the mock mailbox through the pipeline.");

api.MapDelete("/cases", (CaseStore store, HumanReviewQueue queue) =>
{
    store.Clear();
    queue.Clear();
    return Results.Ok(new { cleared = true });
})
.WithName("ClearCases")
.WithDescription("Clears the case journal and human-review queue (demo reset).");

// ---- Human review queue ----
api.MapGet("/queue", (HumanReviewQueue queue) => Results.Ok(queue.All()))
    .WithName("ListQueue")
    .WithDescription("Lists items routed to the human review queue (uncertain/ambiguous intent).");

api.MapPost("/queue/{caseId}/resolve", async (string caseId, ResolveRequest req, HumanReviewQueue queue, CaseStore store, CancellationToken ct) =>
{
    var item = queue.Resolve(caseId, req.Intent ?? "", req.ResolvedBy ?? "reviewer");
    if (item is null) return Results.NotFound(new { error = "Queue item not found", caseId });
    // Reflect the human decision back onto the stored case.
    var c = store.Get(caseId);
    if (c is not null)
    {
        c.Intent.Intent = item.ResolvedIntent ?? c.Intent.Intent;
        c.Intent.RequiresHuman = false;
        c.Intent.IntentBand = "Confirmed (human)";
        c.Intent.SuggestedQueue = c.Intent.Intent;
        c.Status = "completed";
        c.Outcome.FinalStatus = "Resolved (human-confirmed)";
        c.Outcome.AuditTrail.Add(new AuditEntry { Step = "Human review", Detail = $"{item.ResolvedBy} confirmed intent '{item.ResolvedIntent}'." });
        c.Trace.Add(new AgentStep { Stage = "Human Review", Agent = item.ResolvedBy ?? "reviewer", Engine = "human", Decision = item.ResolvedIntent ?? "", Summary = $"Reviewer confirmed intent '{item.ResolvedIntent}'." });
        store.Save(c);
    }
    return Results.Ok(item);
})
.WithName("ResolveQueueItem")
.WithDescription("Resolves a human-review item by confirming the intent; updates the stored case.")
.DisableAntiforgery();

app.Run();

static object ToListItem(EmailCase c) => new
{
    c.CaseId,
    c.Reference,
    c.CreatedUtc,
    c.Status,
    c.Engine,
    from = c.Source.FromName,
    subject = c.Source.Subject,
    category = c.Triage.Category,
    urgency = c.Triage.Urgency,
    intent = c.Intent.Intent,
    intentConfidence = c.Intent.IntentConfidence,
    requiresHuman = c.Intent.RequiresHuman,
    account = c.Task.Customer.AccountName,
    operation = c.Task.Plan.Operation,
    executionStatus = c.Task.ExecutionStatus,
    finalStatus = c.Outcome.FinalStatus
};

public sealed record ResolveRequest(string? Intent, string? ResolvedBy);

// Exposed for integration tests / WebApplicationFactory.
public partial class Program { }
