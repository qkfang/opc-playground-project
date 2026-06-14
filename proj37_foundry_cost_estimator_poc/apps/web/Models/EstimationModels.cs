using System.Text.Json.Serialization;

namespace Proj37.CostEstimator.Web.Models;

/// <summary>
/// The full result of an estimation run, persisted per job and rendered in the UI / Excel.
/// </summary>
public sealed class EstimationResult
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "pending"; // pending | running | completed | failed
    public string? Error { get; set; }

    /// <summary>Whether the run used the live Foundry agent or the deterministic offline engine.</summary>
    public string Engine { get; set; } = "offline";

    public List<IngestedDocument> Documents { get; set; } = new();
    public ScopeSummary Scope { get; set; } = new();
    public List<TechnicalRequirement> Requirements { get; set; } = new();
    public CostEstimate Cost { get; set; } = new();

    /// <summary>Raw transcript of the agent reasoning steps (for transparency / audit).</summary>
    public List<AgentStepLog> AgentSteps { get; set; } = new();
}

public sealed class IngestedDocument
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int CharacterCount { get; set; }
    public int WordCount { get; set; }

    /// <summary>Extracted plain text used for grounding. Truncated for very large files.</summary>
    [JsonIgnore]
    public string ExtractedText { get; set; } = "";

    public string? Excerpt { get; set; }
}

/// <summary>Model-extracted understanding of the project scope.</summary>
public sealed class ScopeSummary
{
    public string ProjectName { get; set; } = "";
    public string Overview { get; set; } = "";
    public string BusinessGoal { get; set; } = "";
    public List<string> InScope { get; set; } = new();
    public List<string> OutOfScope { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public string WorkloadProfile { get; set; } = "";      // e.g. "web app + API + data store"
    public string ExpectedScale { get; set; } = "";        // e.g. "~5k MAU, ~50 req/s peak"
    public string DataSensitivity { get; set; } = "";      // e.g. "internal / PII / regulated"
    public string Environment { get; set; } = "production";
}

/// <summary>A single derived technical requirement.</summary>
public sealed class TechnicalRequirement
{
    public string Id { get; set; } = "";                   // REQ-001
    public string Category { get; set; } = "";             // Compute | Data | Networking | Security | AI | Observability
    public string Requirement { get; set; } = "";
    public string Rationale { get; set; } = "";
    public string Priority { get; set; } = "Should";       // Must | Should | Could
}

/// <summary>The Azure cost estimate, composed of per-service line items.</summary>
public sealed class CostEstimate
{
    public string Currency { get; set; } = "USD";
    public string Region { get; set; } = "australiaeast";
    public string PricingBasis { get; set; } = "Pay-As-You-Go, list price (POC reference rates)";
    public List<CostLineItem> LineItems { get; set; } = new();

    public decimal MonthlyTotal => Math.Round(LineItems.Sum(i => i.MonthlyCost), 2);
    public decimal AnnualTotal => Math.Round(MonthlyTotal * 12m, 2);

    /// <summary>Contingency percentage applied on top of the raw total (risk buffer).</summary>
    public decimal ContingencyPercent { get; set; } = 20m;
    public decimal MonthlyTotalWithContingency => Math.Round(MonthlyTotal * (1 + ContingencyPercent / 100m), 2);

    public List<string> Notes { get; set; } = new();
}

public sealed class CostLineItem
{
    public string Service { get; set; } = "";              // "Azure App Service"
    public string Sku { get; set; } = "";                  // "P1v3"
    public string Meter { get; set; } = "";                // "vCPU-hours" / "GB-month"
    public string Assumption { get; set; } = "";           // "1 instance, 730 hrs/mo"
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }                 // reference unit price
    public string Unit { get; set; } = "";                 // "per instance/mo"
    public decimal MonthlyCost { get; set; }
    public string Category { get; set; } = "";             // Compute | AI | Data | Networking | Security | Observability
}

public sealed class AgentStepLog
{
    public string Step { get; set; } = "";                 // "scope" | "requirements" | "cost"
    public string Summary { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
