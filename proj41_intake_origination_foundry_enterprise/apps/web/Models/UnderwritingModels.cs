using System.Text.Json.Serialization;

namespace Proj41.Underwriting.Web.Models;

// ============================================================================
//  Sentinel Underwriting — Commercial Insurance Submission Desk
//  Domain model for broker submission intake -> underwriting origination.
//
//  Mapping to the required intake/origination entities:
//    Lead        -> Producer (broker/agent who submitted the risk)
//    Account     -> Insured  (the company seeking coverage)
//    Opportunity -> Risk Submission (the line of business + coverage being quoted)
// ============================================================================

/// <summary>An inbound broker submission email that triggers the underwriting pipeline.</summary>
public sealed class SubmissionEmail
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>email | broker-portal | api</summary>
    public string Channel { get; set; } = "email";
    public DateTimeOffset ReceivedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Attachments { get; set; } = new();
}

// ---------------------------------------------------------------- Records ---

/// <summary>The producer / broker contact (the "Lead").</summary>
public sealed class Producer
{
    public string ContactName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Brokerage { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    /// <summary>National | Regional | Independent | Wholesale | Unknown</summary>
    public string BrokerTier { get; set; } = "Unknown";
    /// <summary>Whether this producer is appointed/known to the carrier.</summary>
    public bool Appointed { get; set; }
    public double Confidence { get; set; }
}

/// <summary>The insured company seeking coverage (the "Account").</summary>
public sealed class Insured
{
    public string CompanyName { get; set; } = "";
    public string Industry { get; set; } = "";
    /// <summary>2-digit SIC-style sector grouping inferred from the industry.</summary>
    public string SicDivision { get; set; } = "";
    public string Headquarters { get; set; } = "";
    public string Country { get; set; } = "";
    public int? EmployeeCount { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int LocationCount { get; set; } = 1;
    /// <summary>Total insurable value across all locations (property + contents + BI).</summary>
    public decimal? TotalInsurableValue { get; set; }
    public int YearsInBusiness { get; set; }
    public List<string> Enrichment { get; set; } = new();
    public double Confidence { get; set; }
}

/// <summary>The risk submission being quoted (the "Opportunity").</summary>
public sealed class RiskSubmission
{
    /// <summary>Property | General Liability | Cyber | Professional Liability | Workers Comp | Marine | Multi-line</summary>
    public string LineOfBusiness { get; set; } = "";
    public string CoverageType { get; set; } = "";
    public decimal? RequestedLimit { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? EstimatedAnnualPremium { get; set; }
    public DateTimeOffset? EffectiveDate { get; set; }
    /// <summary>New Business | Renewal | Rewrite</summary>
    public string SubmissionType { get; set; } = "New Business";
    /// <summary>Names of incumbent/competing carriers if mentioned.</summary>
    public List<string> IncumbentCarriers { get; set; } = new();
    public double Confidence { get; set; }
}

public sealed class ExtractedRecords
{
    public Producer Producer { get; set; } = new();
    public Insured Insured { get; set; } = new();
    public RiskSubmission Submission { get; set; } = new();
    public List<string> MissingForUnderwriting { get; set; } = new();

    /// <summary>Raw subject+body text retained for downstream appetite/spam checks (not shown in UI cards).</summary>
    public string RawText { get; set; } = "";
}

// ---------------------------------------------------------------- Triage ----

/// <summary>Appetite + risk triage decision (the classification stage).</summary>
public sealed class AppetiteDecision
{
    /// <summary>In Appetite | Refer to Underwriter | Out of Appetite | Decline</summary>
    public string AppetiteClass { get; set; } = "";
    /// <summary>Quote | Refer | Decline (recommended next action).</summary>
    public string Recommendation { get; set; } = "";
    /// <summary>0-100 composite risk score (higher = riskier/more scrutiny).</summary>
    public int RiskScore { get; set; }
    /// <summary>0-100 desirability/fit score (higher = more attractive account).</summary>
    public int FitScore { get; set; }
    /// <summary>P1 (fast-track) | P2 | P3 — work-queue priority.</summary>
    public string Priority { get; set; } = "P3";
    /// <summary>Target turnaround SLA in business hours.</summary>
    public int SlaHours { get; set; } = 72;
    /// <summary>Underwriting unit / queue the case is routed to.</summary>
    public string RoutingQueue { get; set; } = "";
    /// <summary>Named underwriting authority / desk.</summary>
    public string AssignedDesk { get; set; } = "";
    public bool Declined { get; set; }
    /// <summary>Triggers that force a referral to a senior underwriter.</summary>
    public List<string> ReferralTriggers { get; set; } = new();
    public List<string> RiskFlags { get; set; } = new();
    public string Rationale { get; set; } = "";
}

// --------------------------------------------------------------- Research ---

/// <summary>An inbound demand / exposure signal discovered during research.</summary>
public sealed class ExposureSignal
{
    /// <summary>CatastropheExposure | LossHistory | IndustryHazard | FinancialStress | Regulatory | Growth</summary>
    public string Category { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    /// <summary>Positive | Neutral | Adverse — direction for underwriting.</summary>
    public string Sentiment { get; set; } = "Neutral";
    /// <summary>Low | Medium | High weight on the decision.</summary>
    public string Impact { get; set; } = "Medium";
}

public sealed class LeadResearch
{
    public string AccountOverview { get; set; } = "";
    /// <summary>Estimated buying/binding intent 0-100 (urgency + fit + budget).</summary>
    public int IntentScore { get; set; }
    public string IntentBand { get; set; } = "";
    public List<string> ExposureHighlights { get; set; } = new();
    public List<ExposureSignal> Signals { get; set; } = new();
    public List<string> RecommendedQuestions { get; set; } = new();
}

// ----------------------------------------------------------------- Report ---

public sealed class ReportSection
{
    public string Heading { get; set; } = "";
    public string Body { get; set; } = "";
}

/// <summary>The executive Underwriting Risk Study (the report/study stage).</summary>
public sealed class UnderwritingStudy
{
    public string Title { get; set; } = "";
    public string ExecutiveSummary { get; set; } = "";
    /// <summary>Bind | Quote with conditions | Refer | Decline.</summary>
    public string OverallRecommendation { get; set; } = "";
    public decimal? IndicatedPremium { get; set; }
    public string PricingRationale { get; set; } = "";
    public List<string> KeyRiskFlags { get; set; } = new();
    public List<string> RecommendedConditions { get; set; } = new();
    public List<string> Exclusions { get; set; } = new();
    public List<ReportSection> Sections { get; set; } = new();
    public List<string> NextActions { get; set; } = new();
}

// ------------------------------------------------------------------ Trace ---

/// <summary>One agent step in the pipeline, surfaced as an audit timeline.</summary>
public sealed class AgentTrace
{
    public string Stage { get; set; } = "";
    public string Agent { get; set; } = "";
    /// <summary>foundry | offline</summary>
    public string Engine { get; set; } = "offline";
    public string Summary { get; set; } = "";
    public int DurationMs { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

// ----------------------------------------------------------- Diagnostics ---

/// <summary>
/// Result of actively probing the engine. Distinguishes the four operating modes the
/// captain asked the health surface to make explicit (without leaking secrets):
///   <c>live</c>     — Foundry agent path is configured AND a real round-trip succeeded.
///   <c>fallback</c> — Foundry is configured but the live probe failed; runs use offline per-stage.
///   <c>error</c>    — Foundry is configured but initialisation itself failed.
///   <c>offline</c>  — Foundry is not configured (offline pipeline is the only path by design).
/// </summary>
public sealed class EngineDiagnostics
{
    /// <summary>live | fallback | error | offline</summary>
    public string FoundryMode { get; set; } = "offline";
    /// <summary>True only when a real Foundry agent round-trip succeeded.</summary>
    public bool FoundryLive { get; set; }
    /// <summary>Whether Foundry is configured (Enabled + endpoint present).</summary>
    public bool FoundryConfigured { get; set; }
    /// <summary>Whether the Foundry master switch is on.</summary>
    public bool FoundryEnabled { get; set; }
    /// <summary>Host portion of the configured project endpoint (no path/secrets), or null.</summary>
    public string? EndpointHost { get; set; }
    /// <summary>Configured model deployment name (not a secret).</summary>
    public string? ModelDeployment { get; set; }
    /// <summary>Probe round-trip latency in ms when a probe ran.</summary>
    public int? ProbeMs { get; set; }
    /// <summary>Short, secret-free detail (e.g. exception type) explaining fallback/error.</summary>
    public string? Detail { get; set; }
    public DateTimeOffset CheckedUtc { get; set; } = DateTimeOffset.UtcNow;
}

// ------------------------------------------------------------------- Case ---

public sealed class SubmissionCase
{
    public string CaseId { get; set; } = Guid.NewGuid().ToString("N")[..10];
    /// <summary>Underwriting reference shown to humans, e.g. SUB-2026-00042.</summary>
    public string Reference { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>completed | declined</summary>
    public string Status { get; set; } = "completed";
    /// <summary>foundry | offline (engine that produced this case).</summary>
    public string Engine { get; set; } = "offline";

    public SubmissionEmail Source { get; set; } = new();
    public ExtractedRecords Records { get; set; } = new();
    public AppetiteDecision Triage { get; set; } = new();
    public LeadResearch Research { get; set; } = new();
    public UnderwritingStudy Study { get; set; } = new();
    public List<AgentTrace> Trace { get; set; } = new();
}
