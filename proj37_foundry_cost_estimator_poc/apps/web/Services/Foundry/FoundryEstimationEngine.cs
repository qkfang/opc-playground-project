using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Proj37.CostEstimator.Web.Models;

namespace Proj37.CostEstimator.Web.Services.Foundry;

/// <summary>
/// Estimation engine backed by a Microsoft Foundry prompt agent (Microsoft Agent Framework, hosted
/// in-process pattern via <c>AIProjectClient.AsAIAgent(...)</c>).
///
/// Pipeline (three grounded prompt-agent calls, each returning JSON):
///   1. SCOPE        — read the documents, summarise scope/workload/scale/data-sensitivity.
///   2. REQUIREMENTS — derive technical requirements from the scope + documents.
///   3. SERVICES     — propose the concrete Azure service plan (services, SKUs, quantities).
/// The proposed service plan is then costed locally via <see cref="AzurePricingCatalog"/> so the
/// arithmetic is deterministic and auditable (the model decides architecture; we own the math).
///
/// On ANY failure (missing config, auth, transient service error) it transparently falls back to the
/// offline engine and records the reason, so the POC is always demonstrable.
/// </summary>
public sealed class FoundryEstimationEngine : IEstimationEngine
{
    private readonly FoundryOptions _options;
    private readonly OfflineEstimationEngine _offline;
    private readonly ILogger<FoundryEstimationEngine> _logger;

    public FoundryEstimationEngine(FoundryOptions options, OfflineEstimationEngine offline, ILogger<FoundryEstimationEngine> logger)
    {
        _options = options;
        _offline = offline;
        _logger = logger;
    }

    public string Name => "foundry";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<EstimationResult> EstimateAsync(EstimationResult job, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Foundry not configured; using offline engine.");
            await _offline.EstimateAsync(job, ct);
            job.AgentSteps.Insert(0, new AgentStepLog { Step = "engine", Summary = "Foundry disabled/unconfigured — used deterministic offline engine." });
            return job;
        }

        try
        {
            var agent = CreateAgent();
            var corpus = BuildCorpus(job);

            // 1) SCOPE
            var scope = await RunJsonAsync<ScopeSummary>(agent, ScopePrompt(corpus), ct);
            job.Scope = scope ?? throw new InvalidOperationException("Scope step returned no JSON.");
            NormalizeScope(job.Scope);
            job.AgentSteps.Add(new AgentStepLog { Step = "scope", Summary = $"Foundry agent summarised scope: {job.Scope.WorkloadProfile}." });

            // 2) REQUIREMENTS
            var reqWrap = await RunJsonAsync<RequirementsWrapper>(agent, RequirementsPrompt(corpus, job.Scope), ct);
            job.Requirements = reqWrap?.Requirements ?? new();
            RenumberRequirements(job.Requirements);
            job.AgentSteps.Add(new AgentStepLog { Step = "requirements", Summary = $"Foundry agent derived {job.Requirements.Count} requirements." });

            // 3) SERVICE PLAN -> local costing
            var plan = await RunJsonAsync<ServicePlan>(agent, ServicePlanPrompt(corpus, job.Scope), ct);
            job.Cost = CostFromPlan(plan, job.Scope);
            job.AgentSteps.Add(new AgentStepLog { Step = "cost", Summary = $"Foundry agent proposed {job.Cost.LineItems.Count} services; costed locally to {job.Cost.Currency} {job.Cost.MonthlyTotalWithContingency:N2}/mo (incl. contingency)." });

            job.Engine = Name;
            job.Status = "completed";
            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Foundry estimation failed; falling back to offline engine.");
            // Reset partial state and fall back so the user still gets a complete result.
            job.Scope = new();
            job.Requirements = new();
            job.Cost = new();
            job.AgentSteps.Clear();
            await _offline.EstimateAsync(job, ct);
            job.AgentSteps.Insert(0, new AgentStepLog { Step = "engine", Summary = $"Foundry call failed ({ex.GetType().Name}); fell back to offline engine. Detail: {Trunc(ex.Message, 200)}" });
            return job;
        }
    }

    private AIAgent CreateAgent()
    {
        var client = new AIProjectClient(new Uri(_options.ProjectEndpoint!), new DefaultAzureCredential());
        return client.AsAIAgent(
            model: _options.ModelDeploymentName,
            instructions: AgentInstructions.SystemPersona,
            name: _options.AgentName);
    }

    private static string BuildCorpus(EstimationResult job)
    {
        var docs = string.Join("\n\n", job.Documents.Select(d => $"=== FILE: {d.FileName} ({d.WordCount} words) ===\n{d.ExtractedText}"));
        return Trunc(docs, 48_000);
    }

    private async Task<T?> RunJsonAsync<T>(AIAgent agent, string prompt, CancellationToken ct)
    {
        var response = await agent.RunAsync(prompt, cancellationToken: ct);
        var text = response.Text ?? string.Empty;
        var json = ExtractJsonObject(text);
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }

    /// <summary>Robustly pull the first {...} JSON object out of model output (handles stray fences/prose).</summary>
    private static string? ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text.Substring(start, end - start + 1);
    }

    // ---------------- Prompts ----------------

    private static string ScopePrompt(string corpus) =>
        $$"""
        Analyze the following technical document(s) and produce a SCOPE summary.

        Return JSON with exactly these fields:
        {
          "projectName": string,
          "overview": string,
          "businessGoal": string,
          "inScope": string[],
          "outOfScope": string[],
          "assumptions": string[],
          "workloadProfile": string,        // e.g. "web front end + API + AI workload + relational data store"
          "expectedScale": string,          // e.g. "~5k MAU, ~50 req/s peak"
          "dataSensitivity": string,        // e.g. "internal business data" | "contains PII" | "regulated"
          "environment": string             // "production" | "non-production (POC/dev)"
        }

        DOCUMENTS:
        {{corpus}}
        """;

    private static string RequirementsPrompt(string corpus, ScopeSummary scope) =>
        $$"""
        Given this SCOPE and the source documents, derive the technical requirements for an Azure solution.

        SCOPE: {{JsonSerializer.Serialize(scope, JsonOpts)}}

        Return JSON: { "requirements": [ {
          "id": "REQ-001",
          "category": "Compute|Data|Networking|Security|AI|Observability",
          "requirement": string,
          "rationale": string,
          "priority": "Must|Should|Could"
        } ] }

        Cover compute, data, AI/Foundry (if relevant), security (managed identity, Key Vault), networking
        (HTTPS-only), and observability. 8-14 requirements.

        DOCUMENTS:
        {{corpus}}
        """;

    private static string ServicePlanPrompt(string corpus, ScopeSummary scope) =>
        $$"""
        Design the concrete Azure service plan to run this workload for ONE month, then estimate quantities.
        You decide services/SKUs/quantities; do NOT compute dollar costs (we price them separately).

        SCOPE: {{JsonSerializer.Serialize(scope, JsonOpts)}}

        Return JSON: { "services": [ {
          "service": string,            // e.g. "Azure App Service"
          "sku": string,                // e.g. "P1v3"
          "category": "Compute|AI|Data|Networking|Security|Observability",
          "meter": string,              // e.g. "instance-month", "1K input tokens", "GB-month"
          "assumption": string,         // sizing rationale
          "quantity": number,           // monthly quantity for the meter
          "unitPrice": number,          // your best-estimate USD reference unit price for the meter
          "unit": string                // e.g. "per instance/mo", "per 1K tokens", "per GB/mo"
        } ],
          "contingencyPercent": number  // 15-30, risk buffer
        }

        Always include: compute (App Service), storage (Blob), observability (Log Analytics), security
        (Key Vault). Include Foundry/Azure OpenAI token line items if the workload uses AI, and Azure AI
        Search if it uses document/file search.

        DOCUMENTS:
        {{corpus}}
        """;

    // ---------------- Plan -> Cost ----------------

    private static CostEstimate CostFromPlan(ServicePlan? plan, ScopeSummary scope)
    {
        var est = new CostEstimate
        {
            Currency = AzurePricingCatalog.Currency,
            Region = AzurePricingCatalog.Region,
            PricingBasis = "Foundry-proposed plan, priced with POC reference rates",
            ContingencyPercent = plan?.ContingencyPercent is >= 10 and <= 40 ? plan.ContingencyPercent : 20m,
            Notes =
            {
                "Service plan proposed by Microsoft Foundry prompt agent; unit prices are reference estimates.",
                "Validate against the Azure Pricing Calculator / Retail Prices API before commitment.",
                $"Scale assumption: {scope.ExpectedScale}"
            }
        };

        var services = plan?.Services ?? new();
        foreach (var sp in services)
        {
            var qty = sp.Quantity;
            // Prefer our catalog price for known App Service SKUs to keep numbers grounded; otherwise trust the model.
            var unit = sp.UnitPrice;
            if (sp.Service.Contains("App Service", StringComparison.OrdinalIgnoreCase) &&
                AzurePricingCatalog.AppServicePlanMonthly.TryGetValue(sp.Sku, out var catalogPrice))
            {
                unit = catalogPrice;
            }
            est.LineItems.Add(new CostLineItem
            {
                Service = sp.Service,
                Sku = sp.Sku,
                Meter = sp.Meter,
                Assumption = sp.Assumption,
                Quantity = qty,
                UnitPrice = unit,
                Unit = sp.Unit,
                Category = string.IsNullOrWhiteSpace(sp.Category) ? "Other" : sp.Category,
                MonthlyCost = Math.Round(qty * unit, 2)
            });
        }

        if (est.LineItems.Count == 0)
        {
            est.Notes.Add("Foundry returned no service line items; supplementing with baseline storage/observability.");
            est.LineItems.Add(new CostLineItem { Service = "Azure Blob Storage", Sku = "Hot LRS", Meter = "GB-month", Assumption = "Baseline artefact storage", Quantity = 20, UnitPrice = AzurePricingCatalog.BlobHotPerGbMonth, Unit = "per GB/mo", Category = "Data", MonthlyCost = Math.Round(20 * AzurePricingCatalog.BlobHotPerGbMonth, 2) });
        }

        return est;
    }

    private static void NormalizeScope(ScopeSummary s)
    {
        s.ProjectName = string.IsNullOrWhiteSpace(s.ProjectName) ? "Untitled POC" : s.ProjectName;
        s.WorkloadProfile = string.IsNullOrWhiteSpace(s.WorkloadProfile) ? "web workload" : s.WorkloadProfile;
        s.Environment = string.IsNullOrWhiteSpace(s.Environment) ? "production" : s.Environment;
    }

    private static void RenumberRequirements(List<TechnicalRequirement> reqs)
    {
        for (int i = 0; i < reqs.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(reqs[i].Id)) reqs[i].Id = $"REQ-{i + 1:000}";
            if (string.IsNullOrWhiteSpace(reqs[i].Priority)) reqs[i].Priority = "Should";
            if (string.IsNullOrWhiteSpace(reqs[i].Category)) reqs[i].Category = "Other";
        }
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // ---------------- DTOs for model JSON ----------------

    private sealed class RequirementsWrapper
    {
        [JsonPropertyName("requirements")] public List<TechnicalRequirement> Requirements { get; set; } = new();
    }

    private sealed class ServicePlan
    {
        [JsonPropertyName("services")] public List<ServicePlanItem> Services { get; set; } = new();
        [JsonPropertyName("contingencyPercent")] public decimal ContingencyPercent { get; set; } = 20m;
    }

    private sealed class ServicePlanItem
    {
        [JsonPropertyName("service")] public string Service { get; set; } = "";
        [JsonPropertyName("sku")] public string Sku { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("meter")] public string Meter { get; set; } = "";
        [JsonPropertyName("assumption")] public string Assumption { get; set; } = "";
        [JsonPropertyName("quantity")] public decimal Quantity { get; set; }
        [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
        [JsonPropertyName("unit")] public string Unit { get; set; } = "";
    }
}
