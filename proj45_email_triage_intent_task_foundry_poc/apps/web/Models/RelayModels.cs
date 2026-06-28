using System.Text.Json.Serialization;

namespace Proj45.RelayDesk.Web.Models;

// ============================================================================
//  Relay Desk — Inbound Email Orchestration (proj45)
//  Domain model for: mailbox watch -> extraction -> triage -> intent ->
//  task (D365 MCP) -> outcome.
//
//  Pipeline stages (one routable page each):
//    1. Email    -> IncomingEmail + EmailExtraction
//    2. Triage   -> TriageResult
//    3. Intent   -> IntentDecision (routes uncertain cases to a human queue)
//    4. Task     -> TaskExecution (mock D365 MCP lookups + downstream operation)
//    5. Outcome  -> OutcomeReport (final status + audit trail)
// ============================================================================

/// <summary>An inbound email observed in the watched mailbox; triggers the pipeline.</summary>
public sealed class IncomingEmail
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>email | webform | chat-transcript</summary>
    public string Channel { get; set; } = "email";
    /// <summary>The shared mailbox the message landed in (support@, service@, sales@…).</summary>
    public string Mailbox { get; set; } = "support@relay-desk.example";
    public DateTimeOffset ReceivedUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool Unread { get; set; } = true;
    public List<string> Attachments { get; set; } = new();
}

// -------------------------------------------------------- 1. Extraction -----

/// <summary>Structured extraction of the inbound email (the "Email" stage output).</summary>
public sealed class EmailExtraction
{
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
    public string Subject { get; set; } = "";
    /// <summary>email | webform | chat-transcript</summary>
    public string Channel { get; set; } = "email";
    /// <summary>Detected ISO language code (best-effort), e.g. "en".</summary>
    public string Language { get; set; } = "en";
    /// <summary>Named entities / key phrases pulled from the message.</summary>
    public List<string> Entities { get; set; } = new();
    /// <summary>Order / invoice / reference numbers found in the text.</summary>
    public List<string> OrderRefs { get; set; } = new();
    /// <summary>Hints used to match the sender to a D365 account (company, domain, name).</summary>
    public List<string> AccountHints { get; set; } = new();
    /// <summary>Cleaned, summarised body the downstream agents reason over.</summary>
    public string NormalizedBody { get; set; } = "";
    /// <summary>0..1 confidence the extraction is complete/accurate.</summary>
    public double ExtractionConfidence { get; set; }
}

// ------------------------------------------------------------ 2. Triage -----

/// <summary>Classification + triage of the inbound item (the "Triage" stage output).</summary>
public sealed class TriageResult
{
    /// <summary>Billing | Cancellation | Technical Support | Sales | Complaint | General | Spam</summary>
    public string Category { get; set; } = "";
    public string SubCategory { get; set; } = "";
    /// <summary>P1 (critical) | P2 | P3 | P4 — handling urgency.</summary>
    public string Urgency { get; set; } = "P3";
    /// <summary>Positive | Neutral | Negative | Angry.</summary>
    public string Sentiment { get; set; } = "Neutral";
    /// <summary>0..1 probability the message is spam / junk.</summary>
    public double SpamRisk { get; set; }
    public List<string> RiskFlags { get; set; } = new();
    /// <summary>Target first-response SLA in business hours.</summary>
    public int SlaHours { get; set; } = 24;
    /// <summary>0..1 confidence in the classification.</summary>
    public double TriageConfidence { get; set; }
    public string Rationale { get; set; } = "";
}

// ------------------------------------------------------------ 3. Intent -----

public sealed class AlternativeIntent
{
    public string Intent { get; set; } = "";
    public double Confidence { get; set; }
}

/// <summary>Intent decision + human-review routing (the "Intent" stage output).</summary>
public sealed class IntentDecision
{
    /// <summary>
    /// The customer's purpose, e.g. Billing Dispute | Cancellation Request | Technical Issue |
    /// Sales Enquiry | Complaint Escalation | Information Request | Renewal | Unknown.
    /// </summary>
    public string Intent { get; set; } = "";
    /// <summary>0..1 confidence in the chosen intent.</summary>
    public double IntentConfidence { get; set; }
    /// <summary>High | Medium | Low band derived from the confidence.</summary>
    public string IntentBand { get; set; } = "Low";
    /// <summary>Runner-up intents the router considered.</summary>
    public List<AlternativeIntent> AlternativeIntents { get; set; } = new();
    /// <summary>True when confidence is below threshold or the case is ambiguous/sensitive.</summary>
    public bool RequiresHuman { get; set; }
    /// <summary>Why the case was routed to a human (when RequiresHuman).</summary>
    public string HumanReason { get; set; } = "";
    /// <summary>The work queue the case should be routed to.</summary>
    public string SuggestedQueue { get; set; } = "";
    public string Rationale { get; set; } = "";
}

// ----------------------------------------------------- 4. Task (D365 MCP) ---

/// <summary>A single mock D365 MCP tool invocation, surfaced as a tool-call card + audit entry.</summary>
public sealed class McpToolCall
{
    public string Tool { get; set; } = "";
    /// <summary>Arguments passed to the tool (small, demo-friendly).</summary>
    public Dictionary<string, string> Arguments { get; set; } = new();
    /// <summary>Compact human summary of what came back.</summary>
    public string ResultSummary { get; set; } = "";
    /// <summary>Raw-ish JSON result payload (string) for the detail drawer.</summary>
    public string ResultJson { get; set; } = "";
    public bool Ok { get; set; } = true;
    public int DurationMs { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The customer context assembled from MCP lookups (shown on the Task page).</summary>
public sealed class CustomerContext
{
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Tier { get; set; } = "";
    public string Industry { get; set; } = "";
    public decimal? AnnualValue { get; set; }
    public string Owner { get; set; } = "";
    public string Status { get; set; } = "";
    public string PrimaryContact { get; set; } = "";
    public int OpenOpportunities { get; set; }
    public int OpenServiceCases { get; set; }
    public bool Matched { get; set; }
    public string MatchNote { get; set; } = "";
}

/// <summary>The task agent's plan for the downstream operation (before execution).</summary>
public sealed class TaskPlan
{
    /// <summary>The MCP tool the agent intends to invoke for the downstream action.</summary>
    public string PlannedTool { get; set; } = "";
    /// <summary>Human label for the operation, e.g. "Raise credit memo".</summary>
    public string Operation { get; set; } = "";
    public Dictionary<string, string> OperationArgs { get; set; } = new();
    public string CustomerSummary { get; set; } = "";
    public string ExpectedEffect { get; set; } = "";
    /// <summary>Low | Medium | High operational risk.</summary>
    public string RiskLevel { get; set; } = "Low";
    /// <summary>Whether a human must approve before the operation is committed.</summary>
    public bool RequiresApproval { get; set; }
    public string Rationale { get; set; } = "";
}

/// <summary>The Task stage output: lookups + plan + the executed (simulated) operation result.</summary>
public sealed class TaskExecution
{
    public CustomerContext Customer { get; set; } = new();
    public TaskPlan Plan { get; set; } = new();
    /// <summary>All MCP tool calls made during this stage (lookups + the operation).</summary>
    public List<McpToolCall> ToolCalls { get; set; } = new();
    /// <summary>executed | simulated | deferred-to-human | skipped</summary>
    public string ExecutionStatus { get; set; } = "simulated";
    /// <summary>The id/reference produced by the downstream operation (case id, memo id…).</summary>
    public string OperationReference { get; set; } = "";
    public string OperationResult { get; set; } = "";
}

// ----------------------------------------------------------- 5. Outcome -----

public sealed class AuditEntry
{
    public string Step { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The final outcome report (the "Outcome" stage output).</summary>
public sealed class OutcomeReport
{
    /// <summary>Resolved | Routed to human | Action taken | Closed - spam | Needs follow-up</summary>
    public string FinalStatus { get; set; } = "";
    /// <summary>A drafted customer-facing reply the agent proposes.</summary>
    public string CustomerReplyDraft { get; set; } = "";
    public string ExecutiveSummary { get; set; } = "";
    public List<AuditEntry> AuditTrail { get; set; } = new();
    public List<string> NextActions { get; set; } = new();
    public bool SlaMet { get; set; } = true;
}

// ------------------------------------------------------------- Trace --------

/// <summary>One agent step in the pipeline, surfaced as the enterprise agent timeline.</summary>
public sealed class AgentStep
{
    public string Stage { get; set; } = "";
    public string Agent { get; set; } = "";
    /// <summary>foundry | offline</summary>
    public string Engine { get; set; } = "offline";
    public string Summary { get; set; } = "";
    /// <summary>The headline decision made at this step (for decision cards).</summary>
    public string Decision { get; set; } = "";
    /// <summary>0..1 confidence at this step (null when not applicable), for confidence cards.</summary>
    public double? Confidence { get; set; }
    public int DurationMs { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

// ----------------------------------------------------------- Diagnostics ---

/// <summary>
/// Result of actively probing the engine. Distinguishes the four operating modes:
///   <c>live</c>     — Foundry path configured AND a real round-trip succeeded.
///   <c>fallback</c> — configured but the live probe failed; runs use offline per-stage.
///   <c>error</c>    — configured but initialisation itself failed.
///   <c>offline</c>  — not configured (offline pipeline is the only path by design).
/// </summary>
public sealed class EngineDiagnostics
{
    public string FoundryMode { get; set; } = "offline";
    public bool FoundryLive { get; set; }
    public bool FoundryConfigured { get; set; }
    public bool FoundryEnabled { get; set; }
    public string? EndpointHost { get; set; }
    public string? ModelDeployment { get; set; }
    public int? ProbeMs { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset CheckedUtc { get; set; } = DateTimeOffset.UtcNow;
}

// -------------------------------------------------------------- Case --------

/// <summary>A fully processed inbound-email case (the unit stored in the journal).</summary>
public sealed class EmailCase
{
    public string CaseId { get; set; } = Guid.NewGuid().ToString("N")[..10];
    /// <summary>Human reference, e.g. RLY-2026-00042.</summary>
    public string Reference { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>completed | awaiting-human | closed-spam</summary>
    public string Status { get; set; } = "completed";
    /// <summary>foundry | offline (engine that produced this case).</summary>
    public string Engine { get; set; } = "offline";

    public IncomingEmail Source { get; set; } = new();
    public EmailExtraction Extraction { get; set; } = new();
    public TriageResult Triage { get; set; } = new();
    public IntentDecision Intent { get; set; } = new();
    public TaskExecution Task { get; set; } = new();
    public OutcomeReport Outcome { get; set; } = new();
    public List<AgentStep> Trace { get; set; } = new();
}

// ------------------------------------------------------- Human queue --------

/// <summary>An item parked in the human review queue (low-confidence / ambiguous intent).</summary>
public sealed class HumanReviewItem
{
    public string CaseId { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Subject { get; set; } = "";
    public string FromName { get; set; } = "";
    public string ProposedIntent { get; set; } = "";
    public double IntentConfidence { get; set; }
    public string Reason { get; set; } = "";
    public string Queue { get; set; } = "";
    public DateTimeOffset QueuedUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>pending | resolved</summary>
    public string Status { get; set; } = "pending";
    public string? ResolvedIntent { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedUtc { get; set; }
}
