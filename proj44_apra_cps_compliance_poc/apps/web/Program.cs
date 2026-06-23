using Proj44.Compliance.Web.Models;
using Proj44.Compliance.Web.Services;
using Proj44.Compliance.Web.Services.Foundry;

var builder = WebApplication.CreateBuilder(args);

// ---- Options (bound from "Foundry" / "Storage"; Foundry path is gated on Enabled + ProjectEndpoint) ----
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FoundryOptions>>().Value);
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value);

// ---- Engines: offline (always) + Foundry (falls back to offline). The active engine is chosen by config. ----
builder.Services.AddSingleton<OfflineComplianceEngine>();
builder.Services.AddSingleton<FoundryComplianceEngine>();
builder.Services.AddSingleton<IComplianceEngine>(sp =>
{
    var opts = sp.GetRequiredService<FoundryOptions>();
    return opts.IsConfigured
        ? sp.GetRequiredService<FoundryComplianceEngine>()
        : sp.GetRequiredService<OfflineComplianceEngine>();
});

// ---- Shared framework store (seeded deterministically at startup). ----
builder.Services.AddSingleton<FrameworkStore>();

builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapOpenApi(); // OpenAPI document at /openapi/v1.json

// JSON options for the framework graph (camelCase, ignore nulls where helpful).
var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

// =====================================================================================
// API SURFACE  (minimal APIs under /api)
// =====================================================================================

// ---- Health ----
app.MapGet("/api/health", (FoundryOptions foundry, IComplianceEngine engine) => Results.Ok(new
{
    status = "healthy",
    engine = engine.Name,                 // "foundry" when configured, else "offline"
    foundryConfigured = foundry.IsConfigured,
    time = DateTimeOffset.UtcNow
}))
.WithName("Health").WithSummary("Service health, active engine and Foundry configuration state.");

// ---- Agent instructions (all six stage agents) ----
app.MapGet("/api/agent-instructions", () => Results.Ok(new
{
    persona = AgentInstructions.Persona,
    order = AgentInstructions.Order,
    stages = AgentInstructions.Stages.Select(s => new
    {
        key = s.Key,
        title = s.Title,
        agent = s.Agent,
        goal = s.Goal,
        instructions = s.Instructions
    })
}))
.WithName("AgentInstructions").WithSummary("Persona + per-stage instructions for the six pipeline agents.");

// ---- Full framework graph ----
app.MapGet("/api/framework", (FrameworkStore store) =>
{
    var fw = store.Current;
    return Results.Json(new
    {
        runId = fw.RunId,
        createdUtc = fw.CreatedUtc,
        engine = fw.Engine,
        status = fw.Status,
        source = fw.Source,
        counts = fw.Counts,
        clauses = fw.Clauses,
        requirements = fw.Requirements,
        policies = fw.Policies,
        standards = fw.Standards,
        controls = fw.Controls,
        agentSteps = fw.AgentSteps
    }, json);
})
.WithName("Framework").WithSummary("The full CPS 230 compliance graph: requirements, policies, standards, controls, mappings and counts.");

// ---- Layer collections ----
app.MapGet("/api/requirements", (FrameworkStore store) => Results.Json(store.Current.Requirements, json))
   .WithName("Requirements");
app.MapGet("/api/policies", (FrameworkStore store) => Results.Json(store.Current.Policies, json))
   .WithName("Policies");
app.MapGet("/api/standards", (FrameworkStore store) => Results.Json(store.Current.Standards, json))
   .WithName("Standards");
app.MapGet("/api/controls", (FrameworkStore store) => Results.Json(store.Current.Controls, json))
   .WithName("Controls");
app.MapGet("/api/clauses", (FrameworkStore store) => Results.Json(store.Current.Clauses, json))
   .WithName("Clauses");

// ---- Gap analysis ----
app.MapGet("/api/gaps", (FrameworkStore store) =>
{
    var ga = GapAnalyzer.Analyze(store.Current);
    return Results.Json(ga, json);
})
.WithName("Gaps").WithSummary("Orphans at each layer + coverage percentages + plain findings.");

// ---- Traceability for a single requirement ----
app.MapGet("/api/traceability/{requirementId}", (string requirementId, FrameworkStore store) =>
{
    var chain = TraceabilityResolver.Resolve(store.Current, requirementId);
    return chain is null
        ? Results.NotFound(new { error = $"Requirement '{requirementId}' not found." })
        : Results.Json(chain, json);
})
.WithName("Traceability").WithSummary("Full requirement -> policy -> standard -> control chain for one requirement.");

// ---- Run the six-agent pipeline (offline-deterministic; Foundry when configured) ----
app.MapPost("/api/run", async (IComplianceEngine engine, FrameworkStore store, CancellationToken ct) =>
{
    var fw = await engine.BuildAsync(ct);
    store.Set(fw);
    var ga = GapAnalyzer.Analyze(fw);
    return Results.Json(new
    {
        runId = fw.RunId,
        engine = fw.Engine,
        status = fw.Status,
        counts = fw.Counts,
        agentSteps = fw.AgentSteps,
        gaps = new { ga.TotalGaps, ga.Coverage },
        framework = new
        {
            source = fw.Source,
            requirements = fw.Requirements,
            policies = fw.Policies,
            standards = fw.Standards,
            controls = fw.Controls
        }
    }, json);
})
.WithName("Run").WithSummary("Execute the six-stage compliance-mapping pipeline and return the framework + agent step logs.");

app.Run();

/// <summary>Exposed so WebApplicationFactory&lt;Program&gt; can host the app in tests.</summary>
public partial class Program { }
