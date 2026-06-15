using Proj40.IntelligenceResearch.Web.Models;
using Proj40.IntelligenceResearch.Web.Services;
using Proj40.IntelligenceResearch.Web.Services.Foundry;

var builder = WebApplication.CreateBuilder(args);

// ---- Options ----
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
var foundryOptions = builder.Configuration.GetSection(FoundryOptions.SectionName).Get<FoundryOptions>() ?? new FoundryOptions();
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
builder.Services.AddSingleton(foundryOptions);
builder.Services.AddSingleton(storageOptions);

// ---- Telemetry ----
builder.Services.AddApplicationInsightsTelemetry();

// ---- UI + API ----
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ---- Domain services ----
builder.Services.AddSingleton<MockEmailStore>();
builder.Services.AddSingleton<SourceCorpus>();
builder.Services.AddSingleton<OfflineResearchEngine>();
builder.Services.AddSingleton<FoundryResearchEngine>();
builder.Services.AddSingleton<ResearchCaseService>();

// Engine selection: live Foundry only when explicitly enabled + configured; else deterministic offline.
builder.Services.AddSingleton<IResearchEngine>(sp =>
{
    var opts = sp.GetRequiredService<FoundryOptions>();
    return opts.IsConfigured
        ? sp.GetRequiredService<FoundryResearchEngine>()
        : sp.GetRequiredService<OfflineResearchEngine>();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();
app.MapRazorPages();
app.MapOpenApi();

// ============================================ API ============================================

// Health — also reports which engine is live (used by smoke + deploy health check).
app.MapGet("/api/health", (FoundryOptions f) => Results.Ok(new
{
    status = "healthy",
    engine = f.IsConfigured ? "foundry" : "offline",
    foundryConfigured = f.IsConfigured,
    timeUtc = DateTime.UtcNow
}));

// Inbox — list mock emails (with attachment metadata) for the intake tray.
app.MapGet("/api/inbox", (MockEmailStore store) => Results.Ok(
    store.All.Select(e => new
    {
        e.Id, e.From, e.FromName, e.Subject, e.Preview, e.ReceivedUtc,
        hasDocument = e.Document is not null,
        document = e.Document is null ? null : new { e.Document.FileName, e.Document.DocType, e.Document.WordCount }
    })));

// Single email + full document content (for the Inbox reading pane).
app.MapGet("/api/inbox/{id}", (string id, MockEmailStore store) =>
{
    var e = store.GetById(id);
    return e is null ? Results.NotFound(new { error = $"Email '{id}' not found." }) : Results.Ok(e);
});

// Run the full intelligence & research pipeline for a mock email by id.
app.MapPost("/api/process/{id}", async (string id, MockEmailStore store, IResearchEngine engine, ResearchCaseService cases, CancellationToken ct) =>
{
    var email = store.GetById(id);
    if (email is null) return Results.NotFound(new { error = $"Email '{id}' not found." });
    var c = new ResearchCase { EmailId = email.Id, Email = email };
    await engine.RunAsync(c, ct);
    await cases.SaveAsync(c, ct);
    return Results.Ok(c);
});

// Run the pipeline for an ad-hoc email + optional pasted document (the "compose" path).
app.MapPost("/api/process", async (AdHocRequest req, IResearchEngine engine, ResearchCaseService cases, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Body) && string.IsNullOrWhiteSpace(req.DocumentContent))
        return Results.BadRequest(new { error = "Provide an email body and/or document content." });

    var email = new InboundEmail
    {
        Id = "adhoc-" + Guid.NewGuid().ToString("N")[..6],
        From = string.IsNullOrWhiteSpace(req.From) ? "unknown@example.com" : req.From,
        FromName = string.IsNullOrWhiteSpace(req.FromName) ? "Unknown Sender" : req.FromName,
        Subject = req.Subject ?? "(no subject)",
        Body = req.Body ?? "",
        ReceivedUtc = DateTime.UtcNow,
        Document = string.IsNullOrWhiteSpace(req.DocumentContent) ? null : new CustomerDocument
        {
            FileName = string.IsNullOrWhiteSpace(req.DocumentFileName) ? "pasted-document.md" : req.DocumentFileName,
            DocType = string.IsNullOrWhiteSpace(req.DocumentType) ? "Document" : req.DocumentType,
            Content = req.DocumentContent!
        }
    };
    var c = new ResearchCase { EmailId = email.Id, Email = email };
    await engine.RunAsync(c, ct);
    await cases.SaveAsync(c, ct);
    return Results.Ok(c);
});

// Retrieve a persisted case.
app.MapGet("/api/cases/{caseId}", (string caseId, ResearchCaseService cases) =>
{
    var c = cases.Get(caseId);
    return c is null ? Results.NotFound(new { error = $"Case '{caseId}' not found." }) : Results.Ok(c);
});

// List recent cases.
app.MapGet("/api/cases", (ResearchCaseService cases) => Results.Ok(
    cases.List().Select(c => new
    {
        c.CaseId, c.EmailId, c.CreatedUtc, c.Engine,
        org = c.Entities.PrimaryOrganisation,
        subject = c.Email.Subject,
        findings = c.Brief.KeyFindings.Count
    })));

// Download the report email as a text/markdown artifact.
app.MapGet("/api/cases/{caseId}/report", (string caseId, ResearchCaseService cases) =>
{
    var c = cases.Get(caseId);
    if (c is null) return Results.NotFound(new { error = $"Case '{caseId}' not found." });
    var md = string.IsNullOrWhiteSpace(c.ReportEmail.RenderedMarkdown) ? "(no report generated)" : c.ReportEmail.RenderedMarkdown;
    var bytes = System.Text.Encoding.UTF8.GetBytes(md);
    return Results.File(bytes, "text/markdown", $"report-email-{c.CaseId}.md");
});

app.Run();

// ---- DTOs ----
public sealed record AdHocRequest(
    string? From, string? FromName, string? Subject, string? Body,
    string? DocumentFileName, string? DocumentType, string? DocumentContent);

// Exposed for WebApplicationFactory integration tests.
public partial class Program { }
