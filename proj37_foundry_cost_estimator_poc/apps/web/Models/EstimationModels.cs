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

    // ---- Production totals (the headline numbers; preserved for backward compatibility) ----
    public decimal MonthlyTotal => Math.Round(LineItems.Sum(i => i.MonthlyCost), 2);
    public decimal AnnualTotal => Math.Round(MonthlyTotal * 12m, 2);

    /// <summary>Contingency percentage applied on top of the raw total (risk buffer).</summary>
    public decimal ContingencyPercent { get; set; } = 20m;
    public decimal MonthlyTotalWithContingency => Math.Round(MonthlyTotal * (1 + ContingencyPercent / 100m), 2);

    // ---- Environment-split totals (non-prod / prod / total) ----
    // Non-prod models a scaled-down dev/test/POC footprint of the SAME architecture; total = non-prod + prod.
    public decimal NonProdMonthlyTotal => Math.Round(LineItems.Sum(i => i.NonProdMonthlyCost), 2);
    public decimal ProdMonthlyTotal => Math.Round(LineItems.Sum(i => i.ProdMonthlyCost), 2);
    public decimal CombinedMonthlyTotal => Math.Round(NonProdMonthlyTotal + ProdMonthlyTotal, 2);

    public decimal NonProdMonthlyWithContingency => Math.Round(NonProdMonthlyTotal * (1 + ContingencyPercent / 100m), 2);
    public decimal ProdMonthlyWithContingency => Math.Round(ProdMonthlyTotal * (1 + ContingencyPercent / 100m), 2);
    public decimal CombinedMonthlyWithContingency => Math.Round(CombinedMonthlyTotal * (1 + ContingencyPercent / 100m), 2);

    public decimal NonProdAnnualTotal => Math.Round(NonProdMonthlyTotal * 12m, 2);
    public decimal ProdAnnualTotal => Math.Round(ProdMonthlyTotal * 12m, 2);
    public decimal CombinedAnnualTotal => Math.Round(CombinedMonthlyTotal * 12m, 2);

    public List<string> Notes { get; set; } = new();
}

public sealed class CostLineItem
{
    public string Service { get; set; } = "";              // "Azure App Service"
    public string Sku { get; set; } = "";                  // "P1v3"
    public string Meter { get; set; } = "";                // "vCPU-hours" / "GB-month"
    public string Assumption { get; set; } = "";           // "1 instance, 730 hrs/mo"
    public decimal Quantity { get; set; }                  // PRODUCTION monthly quantity for the meter
    public decimal UnitPrice { get; set; }                 // reference unit price
    public string Unit { get; set; } = "";                 // "per instance/mo"
    public decimal MonthlyCost { get; set; }               // production monthly cost (Quantity * UnitPrice)
    public string Category { get; set; } = "";             // Compute | AI | Data | Networking | Security | Observability

    /// <summary>
    /// Non-production monthly quantity for the SAME meter (scaled-down dev/test/POC sizing). When an
    /// engine does not set this explicitly it is derived from <see cref="Quantity"/> via a per-category
    /// non-prod factor, so the non-prod view is always populated and internally consistent.
    /// </summary>
    public decimal NonProdQuantity { get; set; }

    /// <summary>Production monthly cost = Quantity * UnitPrice (rounded). Mirrors <see cref="MonthlyCost"/>.</summary>
    public decimal ProdMonthlyCost => Math.Round(Quantity * UnitPrice, 2);

    /// <summary>Non-production monthly cost = NonProdQuantity * UnitPrice (rounded).</summary>
    public decimal NonProdMonthlyCost => Math.Round(NonProdQuantity * UnitPrice, 2);

    /// <summary>Combined monthly cost across environments (non-prod + prod).</summary>
    public decimal TotalMonthlyCost => Math.Round(ProdMonthlyCost + NonProdMonthlyCost, 2);

    /// <summary>
    /// Microsoft Azure pricing reference for this meter (surfaced in the UI and the Excel workbook so each
    /// cost line is auditable against first-party pricing). Grounded via Microsoft Learn pricing pages.
    /// </summary>
    public string? PricingReferenceUrl { get; set; }

    /// <summary>Short human label for <see cref="PricingReferenceUrl"/> (e.g. "App Service pricing").</summary>
    public string? PricingReferenceLabel { get; set; }
}

public sealed class AgentStepLog
{
    public string Step { get; set; } = "";                 // "scope" | "requirements" | "cost"
    public string Summary { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
