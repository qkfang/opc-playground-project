using Microsoft.Extensions.Options;
using Proj41.Underwriting.Web.Models;
using Proj41.Underwriting.Web.Services;
using Proj41.Underwriting.Web.Services.Foundry;

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
builder.Services.AddSingleton<MailboxService>();
builder.Services.AddSingleton<CaseStore>();
builder.Services.AddSingleton<OfflineUnderwritingPipeline>();

// Pipeline selection: live Foundry prompt agents when configured, else the deterministic offline
// pipeline. The Foundry pipeline already falls back to offline per-stage on any runtime failure.
builder.Services.AddSingleton<IUnderwritingPipeline>(sp =>
{
    var opts = sp.GetRequiredService<FoundryOptions>();
    var offline = sp.GetRequiredService<OfflineUnderwritingPipeline>();
    return opts.IsConfigured
        ? new FoundryUnderwritingPipeline(opts, offline, sp.GetRequiredService<ILogger<FoundryUnderwritingPipeline>>())
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

var api = app.MapGroup("/api").WithTags("Underwriting");

api.MapGet("/health", (IUnderwritingPipeline pipeline) => Results.Ok(new
{
    status = "healthy",
    engine = pipeline.Name,
    foundryConfigured = foundryOptions.IsConfigured,
    service = "Sentinel Underwriting — Submission Desk",
    time = DateTimeOffset.UtcNow
}))
.WithName("GetHealth")
.WithDescription("Liveness/readiness probe and active engine mode.");

api.MapGet("/inbox", (MailboxService mailbox) =>
    Results.Ok(mailbox.Inbox()))
    .WithName("GetInbox")
    .WithDescription("Returns the mock broker-submission mailbox that triggers the underwriting pipeline.");

api.MapGet("/cases", (CaseStore store) =>
    Results.Ok(store.List().Select(ToListItem)))
    .WithName("ListCases")
    .WithDescription("Lists processed submission cases (most recent first).");

api.MapGet("/cases/{caseId}", (string caseId, CaseStore store) =>
{
    var c = store.Get(caseId);
    return c is null ? Results.NotFound(new { error = "Case not found", caseId }) : Results.Ok(c);
})
.WithName("GetCase")
.WithDescription("Returns the full processed case (records, triage, research, study, trace).");

// Process a mailbox submission by id through the multi-agent pipeline.
api.MapPost("/cases/from-inbox/{emailId}", async (string emailId, MailboxService mailbox, IUnderwritingPipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    var email = mailbox.Get(emailId);
    if (email is null) return Results.NotFound(new { error = "Submission not found", emailId });
    var c = await pipeline.RunAsync(email, ct);
    store.Save(c);
    return Results.Ok(c);
})
.WithName("ProcessInboxSubmission")
.WithDescription("Runs the intake → triage → research → study pipeline for a mailbox submission.");

// Process an ad-hoc submission posted from the trigger console.
api.MapPost("/cases", async (SubmissionEmail email, IUnderwritingPipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(email.From) && string.IsNullOrWhiteSpace(email.Body))
        return Results.BadRequest(new { error = "Provide at least 'from' or 'body'." });
    if (string.IsNullOrWhiteSpace(email.Id)) email.Id = Guid.NewGuid().ToString("N")[..10];
    email.ReceivedUtc = DateTimeOffset.UtcNow;
    var c = await pipeline.RunAsync(email, ct);
    store.Save(c);
    return Results.Ok(c);
})
.WithName("ProcessSubmission")
.WithDescription("Runs the underwriting pipeline against an ad-hoc submission payload.")
.DisableAntiforgery();

// Convenience: process the whole demo inbox in one call (great for demos/CI/smoke tests).
api.MapPost("/cases/run-demo", async (MailboxService mailbox, IUnderwritingPipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    var results = new List<object>();
    foreach (var email in mailbox.Inbox())
    {
        var c = await pipeline.RunAsync(email, ct);
        store.Save(c);
        results.Add(ToListItem(c));
    }
    return Results.Ok(new { processed = results.Count, engine = pipeline.Name, cases = results });
})
.WithName("RunDemoInbox")
.WithDescription("Processes every submission in the mock inbox through the pipeline.");

api.MapDelete("/cases", (CaseStore store) =>
{
    store.Clear();
    return Results.Ok(new { cleared = true });
})
.WithName("ClearCases")
.WithDescription("Clears the case journal (demo reset).");

app.Run();

static object ToListItem(SubmissionCase c) => new
{
    c.CaseId,
    c.Reference,
    c.CreatedUtc,
    c.Status,
    c.Engine,
    insured = c.Records.Insured.CompanyName,
    lineOfBusiness = c.Records.Submission.LineOfBusiness,
    appetite = c.Triage.AppetiteClass,
    recommendation = c.Triage.Recommendation,
    priority = c.Triage.Priority,
    riskScore = c.Triage.RiskScore,
    fitScore = c.Triage.FitScore,
    premium = c.Study.IndicatedPremium,
    intent = c.Research.IntentScore
};

// Exposed for integration tests / WebApplicationFactory.
public partial class Program { }
