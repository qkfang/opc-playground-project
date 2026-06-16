using System.Text.Json.Serialization;

namespace Proj40.IntakeOrigination.Web.Models;

// =====================================================================================
// Domain model for the Enterprise Intake & Origination POC.
//
// Business flow (origination pipeline):
//   Inbound email  ->  [Extraction agent]   -> structured Lead / Account / Opportunity
//                  ->  [Triage agent]        -> classification, routing, priority, SLA
//                  ->  [Lead Mgmt agent]     -> firmographic research + demand signals
//                  ->  [Report agent]        -> origination brief / study
//
// Each stage is produced by a Microsoft Foundry prompt agent (or the deterministic offline
// agent when Foundry is not configured) and recorded as an AgentTrace step for auditability.
// =====================================================================================

/// <summary>An inbound email that triggers the origination pipeline (the mock "front door").</summary>
public sealed class InboundEmail
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset ReceivedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Channel { get; set; } = "email"; // email | web-form | partner-referral
    public List<string> Labels { get; set; } = new();
}

// ----------------------------------------------------------------------- Entities ----

/// <summary>The contact / person who originated the inbound demand.</summary>
public sealed class Lead
{
    public string FullName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Seniority { get; set; } = ""; // C-Level | VP | Director | Manager | IC | Unknown
    public bool IsDecisionMaker { get; set; }
    public string PreferredChannel { get; set; } = "email";
}

/// <summary>The company / organisation behind the demand (firmographics).</summary>
public sealed class Account
{
    public string CompanyName { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Segment { get; set; } = ""; // SMB | Mid-Market | Enterprise | Strategic
    public string EmployeeBand { get; set; } = ""; // e.g. "1k-5k"
    public string Region { get; set; } = "";
    public string Country { get; set; } = "";
    public bool IsExistingCustomer { get; set; }
}

/// <summary>The commercial opportunity inferred from the inbound demand.</summary>
public sealed class Opportunity
{
    public string Name { get; set; } = "";
    public string ProductInterest { get; set; } = "";
    public string UseCase { get; set; } = "";
    public string Timeline { get; set; } = ""; // e.g. "this quarter", "evaluating", "2026 H2"
    public decimal EstimatedAnnualValue { get; set; } // ARR estimate, USD
    public string Currency { get; set; } = "USD";
    public string BudgetStatus { get; set; } = ""; // Budgeted | Exploring | Unknown
    public List<string> Competitors { get; set; } = new();
    public string Notes { get; set; } = "";
}

/// <summary>Wraps the three extracted entities plus an extraction confidence score.</summary>
public sealed class ExtractedRecords
{
    public Lead Lead { get; set; } = new();
    public Account Account { get; set; } = new();
    public Opportunity Opportunity { get; set; } = new();

    /// <summary>0-100 model/heuristic confidence that the extraction is reliable.</summary>
    public int Confidence { get; set; }

    /// <summary>Fields the agent could not populate from the email (drives "needs enrichment" UX).</summary>
    public List<string> MissingFields { get; set; } = new();
}

// ------------------------------------------------------------------------- Triage ----

public sealed class TriageDecision
{
    public string Classification { get; set; } = ""; // New Business | Expansion | Renewal | Support | Spam/Disqualified
    public string Priority { get; set; } = "";        // P1 | P2 | P3
    public int LeadScore { get; set; }                // 0-100 (fit x intent)
    public string RoutingQueue { get; set; } = "";    // e.g. "Enterprise AE - APAC"
    public string RecommendedAction { get; set; } = ""; // e.g. "Book discovery call within 24h"
    public int SlaHours { get; set; }                 // response SLA
    public string Rationale { get; set; } = "";
    public List<string> RiskFlags { get; set; } = new(); // e.g. "Competitor mentioned", "No budget"
    public bool Disqualified { get; set; }
}

// ------------------------------------------------------- Lead research / signals ----

public sealed class DemandSignal
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = ""; // Hiring | Funding | TechAdoption | Expansion | Leadership | Regulatory
    public string Detail { get; set; } = "";
    public string Source { get; set; } = "";   // synthesised reference for the POC
    public string Recency { get; set; } = "";   // e.g. "last 30 days"
    public int Strength { get; set; }           // 0-100 contribution to buying intent
}

public sealed class LeadResearch
{
    public string CompanyOverview { get; set; } = "";
    public List<DemandSignal> Signals { get; set; } = new();
    public List<string> KeyInitiatives { get; set; } = new();
    public List<string> TalkingPoints { get; set; } = new();
    public string BuyingStage { get; set; } = ""; // Awareness | Consideration | Decision
    public int IntentScore { get; set; }          // 0-100 aggregate demand-signal strength
}

// ------------------------------------------------------------------------- Report ----

public sealed class OriginationReport
{
    public string Title { get; set; } = "";
    public string ExecutiveSummary { get; set; } = "";
    public List<string> Highlights { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<ReportSection> Sections { get; set; } = new();
    public string NextBestAction { get; set; } = "";
    public string GeneratedBy { get; set; } = "";
    public DateTimeOffset GeneratedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ReportSection
{
    public string Heading { get; set; } = "";
    public string Body { get; set; } = "";
}

// ----------------------------------------------------------------- Orchestration ----

/// <summary>One step of the multi-agent pipeline, captured for the audit trail / timeline UI.</summary>
public sealed class AgentTrace
{
    public string Stage { get; set; } = "";   // extraction | triage | research | report
    public string Agent { get; set; } = "";    // foundry | offline
    public string Summary { get; set; } = "";
    public long DurationMs { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The full case record: the email plus every agent output and the trace.</summary>
public sealed class IntakeCase
{
    public string CaseId { get; set; } = "CASE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "new"; // new | processing | completed | failed | disqualified
    public string Engine { get; set; } = "offline";

    public InboundEmail Email { get; set; } = new();
    public ExtractedRecords Records { get; set; } = new();
    public TriageDecision Triage { get; set; } = new();
    public LeadResearch Research { get; set; } = new();
    public OriginationReport Report { get; set; } = new();
    public List<AgentTrace> Trace { get; set; } = new();

    [JsonIgnore]
    public bool IsComplete => Status is "completed" or "disqualified";
}
