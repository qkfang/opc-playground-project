using Proj39.IntakeOrigination.Web.Models;
using Proj39.IntakeOrigination.Web.Services;
using Proj39.IntakeOrigination.Web.Services.Foundry;

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
builder.Services.AddSingleton<MockEmailStore>();
builder.Services.AddSingleton<OfflineOriginationEngine>();

// Engine selection: live Foundry agent when configured, otherwise deterministic offline engine.
// FoundryOriginationEngine already falls back to offline internally on any runtime failure.
builder.Services.AddSingleton<IOriginationEngine>(sp =>
{
    var opts = sp.GetRequiredService<FoundryOptions>();
    var offline = sp.GetRequiredService<OfflineOriginationEngine>();
    if (opts.IsConfigured)
        return new FoundryOriginationEngine(opts, offline, sp.GetRequiredService<ILogger<FoundryOriginationEngine>>());
    return offline;
});

builder.Services.AddSingleton<OriginationCaseService>();

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

// OpenAPI document (also usable as a Foundry Agent Service OpenAPI tool).
app.MapOpenApi();

app.MapRazorPages();

// ---------------------------------------------------------------- API ----

var api = app.MapGroup("/api").WithTags("IntakeOrigination");

api.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    engine = foundryOptions.IsConfigured ? "foundry" : "offline",
    foundryConfigured = foundryOptions.IsConfigured,
    time = DateTimeOffset.UtcNow
}))
.WithName("GetHealth")
.WithDescription("Liveness/readiness probe and engine mode.");

// ---- Mock inbox (trigger source) ----
api.MapGet("/emails", (MockEmailStore store) => Results.Ok(store.List().Select(e => new
{
    e.Id, e.From, e.FromName, e.Subject, e.Preview, e.ReceivedUtc, attachments = e.Attachments.Count
})))
.WithName("ListEmails")
.WithDescription("Lists the mocked inbound emails that can trigger the intake pipeline.");

api.MapGet("/emails/{id}", (string id, MockEmailStore store) =>
{
    var e = store.Get(id);
    return e is null ? Results.NotFound(new { error = "Email not found", id }) : Results.Ok(e);
})
.WithName("GetEmail")
.WithDescription("Gets a single mocked inbound email.");

// Add a custom inbound email (compose-your-own demo flow).
api.MapPost("/emails", (InboundEmail email, MockEmailStore store) =>
{
    if (string.IsNullOrWhiteSpace(email.From) || string.IsNullOrWhiteSpace(email.Body))
        return Results.BadRequest(new { error = "An inbound email needs at least 'from' and 'body'." });
    var saved = store.Add(email);
    return Results.Created($"/api/emails/{saved.Id}", saved);
})
.WithName("AddEmail")
.WithDescription("Adds a custom inbound email to the mock inbox.")
.DisableAntiforgery();

// ---- Cases (run pipeline + read results) ----
api.MapGet("/cases", (OriginationCaseService svc) => Results.Ok(svc.List().Select(ToListItem)))
.WithName("ListCases")
.WithDescription("Lists all processed origination cases (most recent first).");

api.MapGet("/cases/{caseId}", (string caseId, OriginationCaseService svc) =>
{
    var c = svc.Get(caseId);
    return c is null ? Results.NotFound(new { error = "Case not found", caseId }) : Results.Ok(c);
})
.WithName("GetCase")
.WithDescription("Gets the full origination case: extraction, triage, research, and report.");

api.MapGet("/cases/{caseId}/report", (string caseId, OriginationCaseService svc) =>
{
    if (!svc.TryGetReportMarkdown(caseId, out var md, out var fileName))
        return Results.NotFound(new { error = "Report not found", caseId });
    return Results.File(System.Text.Encoding.UTF8.GetBytes(md), "text/markdown", fileName);
})
.WithName("DownloadReport")
.WithDescription("Downloads the generated origination study as a markdown file.");

// Run the pipeline against an existing mock email by id.
api.MapPost("/cases/process/{emailId}", async (string emailId, MockEmailStore store, OriginationCaseService svc, CancellationToken ct) =>
{
    var email = store.Get(emailId);
    if (email is null) return Results.NotFound(new { error = "Email not found", emailId });
    var result = await svc.RunAsync(email, ct);
    return result.Status == "failed" ? Results.UnprocessableEntity(result) : Results.Ok(result);
})
.WithName("ProcessEmail")
.WithDescription("Runs the full intake & origination pipeline against a mocked inbound email.")
.DisableAntiforgery();

// Run the pipeline against an ad-hoc email body (no need to persist first).
api.MapPost("/cases/process", async (InboundEmail email, OriginationCaseService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(email.Body))
        return Results.BadRequest(new { error = "Provide an inbound email with at least a 'body'." });
    var result = await svc.RunAsync(email, ct);
    return result.Status == "failed" ? Results.UnprocessableEntity(result) : Results.Ok(result);
})
.WithName("ProcessAdHoc")
.WithDescription("Runs the pipeline against an ad-hoc inbound email payload (great for demos/CI).")
.DisableAntiforgery();

app.Run();

static object ToListItem(OriginationCase c) => new
{
    c.CaseId,
    c.CreatedUtc,
    c.Status,
    c.Engine,
    account = c.Extraction.Account.Name,
    lead = c.Extraction.Lead.FullName,
    classification = c.Triage.Classification,
    score = c.Triage.Score,
    routedTo = c.Triage.RoutedTo,
    estimatedValue = c.Extraction.Opportunity.EstimatedValue,
    currency = c.Extraction.Opportunity.Currency
};

// Exposed for integration tests / WebApplicationFactory.
public partial class Program { }
