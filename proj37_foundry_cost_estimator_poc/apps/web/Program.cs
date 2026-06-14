using Proj37.CostEstimator.Web.Models;
using Proj37.CostEstimator.Web.Services;
using Proj37.CostEstimator.Web.Services.Foundry;

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
builder.Services.AddSingleton<DocumentIngestionService>();
builder.Services.AddSingleton<ExcelReportGenerator>();
builder.Services.AddSingleton<OfflineEstimationEngine>();

// Engine selection: live Foundry agent when configured, otherwise deterministic offline engine.
// FoundryEstimationEngine already falls back to offline internally on any runtime failure.
builder.Services.AddSingleton<IEstimationEngine>(sp =>
{
    var opts = sp.GetRequiredService<FoundryOptions>();
    var offline = sp.GetRequiredService<OfflineEstimationEngine>();
    if (opts.IsConfigured)
    {
        return new FoundryEstimationEngine(opts, offline, sp.GetRequiredService<ILogger<FoundryEstimationEngine>>());
    }
    return offline;
});

builder.Services.AddSingleton<EstimationJobService>();

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// File upload size limit (50 MB) for technical documents.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

// OpenAPI document (also usable as a Foundry Agent Service OpenAPI tool per the App Service tutorial).
app.MapOpenApi();

app.MapRazorPages();

// ---------------------------------------------------------------- API ----

var api = app.MapGroup("/api").WithTags("Estimation");

api.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    engine = foundryOptions.IsConfigured ? "foundry" : "offline",
    foundryConfigured = foundryOptions.IsConfigured,
    region = AzurePricingCatalog.Region,
    time = DateTimeOffset.UtcNow
}))
.WithName("GetHealth")
.WithDescription("Liveness/readiness probe and engine mode.");

api.MapGet("/estimations", (EstimationJobService svc) =>
    Results.Ok(svc.List().Select(ToListItem)))
    .WithName("ListEstimations")
    .WithDescription("Lists all estimation jobs (most recent first).");

api.MapGet("/estimations/{jobId}", (string jobId, EstimationJobService svc) =>
{
    var job = svc.Get(jobId);
    return job is null ? Results.NotFound(new { error = "Job not found", jobId }) : Results.Ok(job);
})
.WithName("GetEstimation")
.WithDescription("Gets the full estimation result for a job, including scope, requirements, and costs.");

api.MapGet("/estimations/{jobId}/workbook", (string jobId, EstimationJobService svc) =>
{
    if (!svc.TryGetWorkbook(jobId, out var bytes, out var fileName))
        return Results.NotFound(new { error = "Workbook not found", jobId });
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
})
.WithName("DownloadWorkbook")
.WithDescription("Downloads the generated Excel cost-calculation workbook for a job.");

// Multipart upload -> ingest -> estimate -> return result.
api.MapPost("/estimations", async (HttpRequest request, EstimationJobService svc, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected multipart/form-data with one or more 'files'." });

    var form = await request.ReadFormAsync(ct);
    var uploads = form.Files
        .Select(f => new EstimationJobService.UploadedFile(f.FileName, f.ContentType, f.OpenReadStream()))
        .ToList();

    if (uploads.Count == 0)
        return Results.BadRequest(new { error = "No files uploaded." });

    var job = await svc.CreateAndRunAsync(uploads, ct);
    return job.Status == "failed"
        ? Results.UnprocessableEntity(job)
        : Results.Ok(job);
})
.WithName("CreateEstimation")
.WithDescription("Uploads technical documents, runs the Foundry/offline estimation pipeline, and returns scope, requirements, and Azure cost estimate.")
.DisableAntiforgery();

// Convenience: run an estimation against the bundled sample document (no upload needed; great for demos/CI).
api.MapPost("/estimations/sample", async (EstimationJobService svc, IWebHostEnvironment env, CancellationToken ct) =>
{
    var samplePath = Path.Combine(env.ContentRootPath, "Data", "sample-statement-of-work.md");
    if (!File.Exists(samplePath))
        return Results.NotFound(new { error = "Sample document not found on server." });

    await using var fs = File.OpenRead(samplePath);
    var job = await svc.CreateAndRunAsync(
        new[] { new EstimationJobService.UploadedFile("sample-statement-of-work.md", "text/markdown", fs) }, ct);
    return Results.Ok(job);
})
.WithName("CreateSampleEstimation")
.WithDescription("Runs an estimation against the bundled sample statement of work. Useful for demos and smoke tests.");

app.Run();

static object ToListItem(EstimationResult r) => new
{
    r.JobId,
    r.CreatedUtc,
    r.Status,
    r.Engine,
    project = r.Scope.ProjectName,
    documents = r.Documents.Count,
    requirements = r.Requirements.Count,
    monthlyTotal = r.Cost.MonthlyTotalWithContingency,
    currency = r.Cost.Currency
};

// Exposed for integration tests / WebApplicationFactory.
public partial class Program { }
