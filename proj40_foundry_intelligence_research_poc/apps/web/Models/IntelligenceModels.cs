using System.Text.Json.Serialization;

namespace Proj40.IntelligenceResearch.Web.Models;

// =====================================================================================================
// proj40 — Intelligence & Research agents POC domain model.
//
// End-to-end case flow (each stage is an "agent" step, all traceable):
//   InboundEmail (+ attached CustomerDocument)
//      -> ExtractedEntities          (key people / orgs / topics / amounts / dates / locations / tech)
//      -> List<Insight>              (what the email + document actually mean)
//      -> List<SourceHit>            (mocked internal + external sources pulled by entity)
//      -> ResearchBrief              (Research Agent synthesis: summary, findings, risks, opportunities)
//      -> ReportEmail                (send-ready email that summarises the insights for a stakeholder)
// =====================================================================================================

/// <summary>A mock inbound email that lands in the intake tray, optionally carrying a customer document.</summary>
public sealed class InboundEmail
{
    public string Id { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public string To { get; set; } = "intake@contoso.com";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The customer document attached to this email (the artifact the research pipeline reasons over).</summary>
    public CustomerDocument? Document { get; set; }

    /// <summary>Short preview of the body for the inbox list.</summary>
    [JsonIgnore]
    public string Preview => Body.Length <= 140 ? Body.Replace("\n", " ") : Body.Replace("\n", " ")[..140] + "…";
}

/// <summary>A customer-supplied document (the "attachment") that the Intelligence pipeline analyses.</summary>
public sealed class CustomerDocument
{
    public string FileName { get; set; } = "";
    public string DocType { get; set; } = "";        // e.g. "RFP", "Briefing note", "Annual report extract", "Incident report"
    public string MimeType { get; set; } = "text/markdown";
    public string Content { get; set; } = "";          // extracted text of the document
    public int WordCount => string.IsNullOrWhiteSpace(Content) ? 0 : Content.Split(' ', '\n', '\t').Count(w => w.Length > 0);
}

/// <summary>Key entities extracted from the email + document — the anchors for source enrichment.</summary>
public sealed class ExtractedEntities
{
    public string? PrimaryOrganisation { get; set; }
    public List<string> Organisations { get; set; } = new();
    public List<string> People { get; set; } = new();
    public List<string> Topics { get; set; } = new();          // themes / subject areas
    public List<string> Technologies { get; set; } = new();
    public List<string> Locations { get; set; } = new();
    public List<string> MonetaryAmounts { get; set; } = new();
    public List<string> Dates { get; set; } = new();
    public string? Industry { get; set; }
    public string? Intent { get; set; }                        // why they reached out (one line)

    [JsonIgnore]
    public IEnumerable<string> AllKeyEntities =>
        new[] { PrimaryOrganisation }.Where(s => s is not null)!
        .Concat(Organisations).Concat(People).Concat(Topics).Concat(Technologies)
        .Select(s => s!.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
}

/// <summary>An insight generated from the email + document (stage 2).</summary>
public sealed class Insight
{
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Category { get; set; } = "";    // "Need" | "Risk" | "Opportunity" | "Context" | "Signal"
    public string Confidence { get; set; } = "Medium";  // High | Medium | Low
    public string Evidence { get; set; } = "";    // what in the email/doc supports it (traceability)
}

/// <summary>A record pulled from a mocked internal or external source, keyed off an entity (stage 3).</summary>
public sealed class SourceHit
{
    public string Entity { get; set; } = "";       // the entity that triggered this pull
    public string SourceName { get; set; } = "";   // e.g. "CRM (internal)", "News wire (external)"
    public string SourceType { get; set; } = "";   // "Internal" | "External"
    public string Title { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string? Url { get; set; }
    public DateTime? Dated { get; set; }
    public string Relevance { get; set; } = "Medium";  // High | Medium | Low
}

/// <summary>The Research Agent's synthesised brief (stage 4).</summary>
public sealed class ResearchBrief
{
    public string Title { get; set; } = "";
    public string ExecutiveSummary { get; set; } = "";
    public List<string> KeyFindings { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public List<string> Opportunities { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public List<string> OpenQuestions { get; set; } = new();
    public List<Citation> Citations { get; set; } = new();
    public string Confidence { get; set; } = "Medium";
}

/// <summary>A citation linking a finding back to a source hit (traceable outputs).</summary>
public sealed class Citation
{
    public string Marker { get; set; } = "";       // e.g. "[S1]"
    public string SourceName { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Url { get; set; }
}

/// <summary>The send-ready report email that summarises the insights for a stakeholder (stage 5).</summary>
public sealed class ReportEmail
{
    public string To { get; set; } = "";
    public string Cc { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Greeting { get; set; } = "";
    public string Body { get; set; } = "";           // plain-text email body
    public string CallToAction { get; set; } = "";
    public string Signature { get; set; } = "";
    public string RenderedMarkdown { get; set; } = ""; // full email rendered for download (.md/.eml-ish)
}

/// <summary>A log line for one agent stage (traceability across the pipeline).</summary>
public sealed class AgentStepLog
{
    public string Step { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>The full intelligence case that flows through every agent stage and is persisted.</summary>
public sealed class ResearchCase
{
    public string CaseId { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string EmailId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Engine { get; set; } = "offline";   // "foundry" | "offline"

    public InboundEmail Email { get; set; } = new();
    public ExtractedEntities Entities { get; set; } = new();
    public List<Insight> Insights { get; set; } = new();
    public List<SourceHit> SourceHits { get; set; } = new();
    public ResearchBrief Brief { get; set; } = new();
    public ReportEmail ReportEmail { get; set; } = new();
    public List<AgentStepLog> AgentSteps { get; set; } = new();
}
