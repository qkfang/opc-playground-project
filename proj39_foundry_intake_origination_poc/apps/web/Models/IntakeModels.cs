using System.Text.Json.Serialization;

namespace Proj39.IntakeOrigination.Web.Models;

/// <summary>
/// A mocked inbound email that acts as the trigger source for the intake &amp; origination pipeline.
/// In a real deployment this would arrive from a mailbox connector (Graph / Exchange / Logic Apps).
/// </summary>
public sealed class InboundEmail
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public string To { get; set; } = "intake@contoso.com";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset ReceivedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Attachments { get; set; } = new();

    /// <summary>Short preview for list views.</summary>
    [JsonIgnore]
    public string Preview => Body.Length <= 160 ? Body : Body[..160] + "…";
}

// ----------------------------------------------------------------------------------------------
// Structured CRM-style records extracted from the email.
// ----------------------------------------------------------------------------------------------

public sealed class Account
{
    public string Name { get; set; } = "";
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Domain { get; set; }
    public string? EmployeeBand { get; set; }      // e.g. "1-50", "5,000-10,000"
    public string? AnnualRevenueBand { get; set; } // e.g. "$10M-$50M"
    public string? Country { get; set; }
    public string? Region { get; set; }
    public List<string> Notes { get; set; } = new();
}

public sealed class Lead
{
    public string FullName { get; set; } = "";
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AccountName { get; set; }
    public string? Seniority { get; set; }        // e.g. "C-Level", "Director", "Manager"
    public bool IsDecisionMaker { get; set; }
    public string? PreferredContactMethod { get; set; }
    public List<string> Notes { get; set; } = new();
}

public sealed class Opportunity
{
    public string Name { get; set; } = "";
    public string? ProductInterest { get; set; }
    public string? Summary { get; set; }
    public decimal? EstimatedValue { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Timeline { get; set; }          // e.g. "Q3 FY26", "next 60 days"
    public string? BudgetStatus { get; set; }      // e.g. "budget approved", "exploring"
    public string Stage { get; set; } = "New";     // New | Qualifying | Qualified | Disqualified
    public List<string> Drivers { get; set; } = new();   // business drivers / pain points
    public List<string> Notes { get; set; } = new();
}

/// <summary>The combined structured extraction result for one email.</summary>
public sealed class ExtractionResult
{
    public Account Account { get; set; } = new();
    public Lead Lead { get; set; } = new();
    public Opportunity Opportunity { get; set; } = new();
    public decimal Confidence { get; set; }        // 0..1 overall extraction confidence
    public List<string> MissingFields { get; set; } = new();
}

// ----------------------------------------------------------------------------------------------
// Early triage & classification.
// ----------------------------------------------------------------------------------------------

public sealed class TriageResult
{
    /// <summary>Hot | Warm | Cold | Spam — routing band.</summary>
    public string Classification { get; set; } = "Warm";

    /// <summary>0..100 composite priority score.</summary>
    public int Score { get; set; }

    /// <summary>Where this should be routed, e.g. "Enterprise Sales", "SMB Desk", "Nurture".</summary>
    public string RoutedTo { get; set; } = "Nurture";

    /// <summary>SLA target for first response.</summary>
    public string SlaTarget { get; set; } = "5 business days";

    public string Recommendation { get; set; } = "";

    /// <summary>Transparent score breakdown so the POC is auditable.</summary>
    public List<TriageFactor> Factors { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public sealed class TriageFactor
{
    public string Name { get; set; } = "";
    public int Points { get; set; }
    public string Detail { get; set; } = "";
}

// ----------------------------------------------------------------------------------------------
// Lead Management Agent — research + inbound demand signals.
// ----------------------------------------------------------------------------------------------

public sealed class ResearchResult
{
    public string CompanyOverview { get; set; } = "";
    public List<DemandSignal> DemandSignals { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public List<string> TalkingPoints { get; set; } = new();
    public List<string> Competitors { get; set; } = new();
    public string FitAssessment { get; set; } = "";   // why this is / isn't a good fit
}

public sealed class DemandSignal
{
    public string Signal { get; set; } = "";          // e.g. "Hiring 20+ data engineers"
    public string Source { get; set; } = "";          // e.g. "Careers page (mock)"
    public string Strength { get; set; } = "Medium";  // Strong | Medium | Weak
    public string Implication { get; set; } = "";     // why it matters for this opportunity
}

// ----------------------------------------------------------------------------------------------
// Report Agent — generated report / study.
// ----------------------------------------------------------------------------------------------

public sealed class OriginationReport
{
    public string Title { get; set; } = "";
    public string ExecutiveSummary { get; set; } = "";
    public List<ReportSection> Sections { get; set; } = new();
    public string RecommendedNextStep { get; set; } = "";
    public string Disposition { get; set; } = "Pursue"; // Pursue | Nurture | Disqualify
    public string GeneratedMarkdown { get; set; } = "";  // full report rendered as markdown
}

public sealed class ReportSection
{
    public string Heading { get; set; } = "";
    public string Body { get; set; } = "";
}

// ----------------------------------------------------------------------------------------------
// The end-to-end case that flows through every agent stage.
// ----------------------------------------------------------------------------------------------

public sealed class OriginationCase
{
    public string CaseId { get; set; } = Guid.NewGuid().ToString("n");
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>"completed" | "failed" | "running".</summary>
    public string Status { get; set; } = "running";

    /// <summary>"foundry" | "offline" — which engine produced the reasoning.</summary>
    public string Engine { get; set; } = "offline";

    public InboundEmail Email { get; set; } = new();
    public ExtractionResult Extraction { get; set; } = new();
    public TriageResult Triage { get; set; } = new();
    public ResearchResult Research { get; set; } = new();
    public OriginationReport Report { get; set; } = new();

    /// <summary>Per-agent step log so the multi-agent pipeline is observable in the UI.</summary>
    public List<AgentStepLog> AgentSteps { get; set; } = new();
}

public sealed class AgentStepLog
{
    public string Agent { get; set; } = "";          // Intake | Extraction | Triage | LeadResearch | Report
    public string Step { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Engine { get; set; } = "offline";  // foundry | offline
    public int DurationMs { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
