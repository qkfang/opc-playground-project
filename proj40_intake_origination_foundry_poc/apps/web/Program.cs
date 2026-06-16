using Proj40.IntakeOrigination.Web.Models;
using Proj40.IntakeOrigination.Web.Services;
using Proj40.IntakeOrigination.Web.Services.Foundry;

var builder = WebApplication.CreateBuilder(args);

// ----- Configuration / options -----
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

var foundryOptions = builder.Configuration.GetSection(FoundryOptions.SectionName).Get<FoundryOptions>() ?? new FoundryOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
builder.Services.AddSingleton(foundryOptions);
builder.Services.AddSingleton(storageOptions);

// ----- Telemetry (Application Insights if a connection string is present) -----
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// ----- Core services -----
builder.Services.AddSingleton<MailboxService>();
builder.Services.AddSingleton<CaseStore>();
builder.Services.AddSingleton<OfflineIntakePipeline>();

// Pipeline selection: live Foundry prompt agents when configured, otherwise the deterministic offline
// pipeline. The Foundry pipeline already falls back to offline internally on any runtime failure.
builder.Services.AddSingleton<IIntakePipeline>(sp =>
{
    var opts = sp.GetRequiredService<FoundryOptions>();
    var offline = sp.GetRequiredService<OfflineIntakePipeline>();
    return opts.IsConfigured
        ? new FoundryIntakePipeline(opts, offline, sp.GetRequiredService<ILogger<FoundryIntakePipeline>>())
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

var api = app.MapGroup("/api").WithTags("Intake");

api.MapGet("/health", (IIntakePipeline pipeline) => Results.Ok(new
{
    status = "healthy",
    engine = pipeline.Name,
    foundryConfigured = foundryOptions.IsConfigured,
    time = DateTimeOffset.UtcNow
}))
.WithName("GetHealth")
.WithDescription("Liveness/readiness probe and active engine mode.");

api.MapGet("/inbox", (MailboxService mailbox) =>
    Results.Ok(mailbox.Inbox()))
    .WithName("GetInbox")
    .WithDescription("Returns the mock inbound mailbox that triggers the origination pipeline.");

api.MapGet("/cases", (CaseStore store) =>
    Results.Ok(store.List().Select(ToListItem)))
    .WithName("ListCases")
    .WithDescription("Lists processed intake cases (most recent first).");

api.MapGet("/cases/{caseId}", (string caseId, CaseStore store) =>
{
    var c = store.Get(caseId);
    return c is null ? Results.NotFound(new { error = "Case not found", caseId }) : Results.Ok(c);
})
.WithName("GetCase")
.WithDescription("Returns the full processed case (records, triage, research, report, trace).");

// Process a mailbox email by id through the multi-agent pipeline.
api.MapPost("/cases/from-inbox/{emailId}", async (string emailId, MailboxService mailbox, IIntakePipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    var email = mailbox.Get(emailId);
    if (email is null) return Results.NotFound(new { error = "Email not found", emailId });
    var c = await pipeline.RunAsync(email, ct);
    store.Save(c);
    return Results.Ok(c);
})
.WithName("ProcessInboxEmail")
.WithDescription("Runs the extraction → triage → research → report pipeline for a mailbox email.");

// Process an ad-hoc email posted from the trigger console.
api.MapPost("/cases", async (InboundEmail email, IIntakePipeline pipeline, CaseStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(email.From) && string.IsNullOrWhiteSpace(email.Body))
        return Results.BadRequest(new { error = "Provide at least 'from' or 'body'." });
    if (string.IsNullOrWhiteSpace(email.Id)) email.Id = Guid.NewGuid().ToString("N")[..10];
    email.ReceivedUtc = DateTimeOffset.UtcNow;
    var c = await pipeline.RunAsync(email, ct);
    store.Save(c);
    return Results.Ok(c);
})
.WithName("ProcessEmail")
.WithDescription("Runs the origination pipeline against an ad-hoc inbound email payload.")
.DisableAntiforgery();

// Convenience: process the whole demo inbox in one call (great for demos/CI/smoke tests).
api.MapPost("/cases/run-demo", async (MailboxService mailbox, IIntakePipeline pipeline, CaseStore store, CancellationToken ct) =>
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
.WithDescription("Processes every email in the mock inbox through the pipeline.");

api.MapDelete("/cases", (CaseStore store) =>
{
    store.Clear();
    return Results.Ok(new { cleared = true });
})
.WithName("ClearCases")
.WithDescription("Clears the case journal (demo reset).");

app.Run();

static object ToListItem(IntakeCase c) => new
{
    c.CaseId,
    c.CreatedUtc,
    c.Status,
    c.Engine,
    company = c.Records.Account.CompanyName,
    segment = c.Records.Account.Segment,
    classification = c.Triage.Classification,
    priority = c.Triage.Priority,
    leadScore = c.Triage.LeadScore,
    arr = c.Records.Opportunity.EstimatedAnnualValue,
    intent = c.Research.IntentScore
};

// Exposed for integration tests / WebApplicationFactory.
public partial class Program { }
