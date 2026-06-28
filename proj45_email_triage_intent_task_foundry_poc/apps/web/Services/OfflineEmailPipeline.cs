using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Proj45.RelayDesk.Web.Models;
using Proj45.RelayDesk.Web.Services.Mcp;

namespace Proj45.RelayDesk.Web.Services;

/// <summary>
/// Deterministic, offline implementation of the inbound-email orchestration pipeline. Uses heuristic
/// NLP over the email text so the POC always runs with no Azure/Foundry connectivity. The Foundry
/// pipeline reuses these per-stage methods as a fallback.
///
/// Stages:
///   1. Extraction   -> EmailExtraction
///   2. Triage       -> TriageResult
///   3. Intent       -> IntentDecision  (routes uncertain cases to the human queue)
///   4. Task          -> TaskExecution   (mock D365 MCP lookups + downstream operation)
///   5. Outcome       -> OutcomeReport   (final status + audit trail)
/// </summary>
public sealed partial class OfflineEmailPipeline : IEmailPipeline
{
    private readonly ID365McpServer _mcp;
    private readonly HumanReviewQueue _queue;
    private readonly FoundryOptions _options;

    public string Name => "offline";
    private static int _seq = 45_000;

    public OfflineEmailPipeline(ID365McpServer mcp, HumanReviewQueue queue, FoundryOptions options)
    {
        _mcp = mcp;
        _queue = queue;
        _options = options;
    }

    public Task<EmailCase> RunAsync(IncomingEmail email, CancellationToken ct = default)
    {
        var trace = new List<AgentStep>();

        var extraction = Step(trace, "Extraction", "relay-triage-extraction-agent",
            e => $"Extracted sender, {e.Entities.Count} entities, {e.OrderRefs.Count} ref(s); language {e.Language}.",
            e => "extraction", e => e.ExtractionConfidence,
            () => Extract(email));

        var triage = Step(trace, "Triage", "relay-triage-extraction-agent",
            t => $"{t.Category} / {t.Urgency}; sentiment {t.Sentiment}; spam {t.SpamRisk:0.00}.",
            t => $"{t.Category} ({t.Urgency})", t => t.TriageConfidence,
            () => Triage(extraction, email));

        var intent = Step(trace, "Intent", "relay-intent-router-agent",
            i => i.RequiresHuman ? $"Uncertain → human queue ({i.Intent} {i.IntentConfidence:0.00})." : $"{i.Intent} ({i.IntentConfidence:0.00}); queue {i.SuggestedQueue}.",
            i => i.RequiresHuman ? "Route to human" : i.Intent, i => i.IntentConfidence,
            () => DecideIntent(extraction, triage));

        var task = Step(trace, "Task", "relay-task-execution-agent",
            t => $"{t.Customer.MatchNote}; {t.ToolCalls.Count} MCP call(s); {t.ExecutionStatus} {t.Plan.Operation}.",
            t => t.Plan.Operation, t => null,
            () => RunTask(email, extraction, triage, intent));

        var outcome = Step(trace, "Outcome", "relay-outcome-reporter-agent",
            o => $"{o.FinalStatus}; {o.AuditTrail.Count} audit entries; SLA {(o.SlaMet ? "met" : "at risk")}.",
            o => o.FinalStatus, o => null,
            () => BuildOutcome(email, extraction, triage, intent, task));

        var caseObj = new EmailCase
        {
            Reference = $"RLY-{DateTimeOffset.UtcNow:yyyy}-{Interlocked.Increment(ref _seq):D5}",
            Status = StatusFor(triage, intent),
            Engine = Name,
            Source = email,
            Extraction = extraction,
            Triage = triage,
            Intent = intent,
            Task = task,
            Outcome = outcome,
            Trace = trace
        };

        EnqueueHumanIfNeeded(caseObj);
        return Task.FromResult(caseObj);
    }

    public Task<EngineDiagnostics> ProbeAsync(CancellationToken ct = default) =>
        Task.FromResult(new EngineDiagnostics
        {
            FoundryMode = "offline",
            FoundryLive = false,
            FoundryConfigured = false,
            FoundryEnabled = false,
            Detail = "Foundry not configured; deterministic offline engine is active by design."
        });

    // ---- Per-stage accessors so the Foundry pipeline can fall back stage-by-stage. ----
    internal EmailExtraction ExtractStage(IncomingEmail e) => Extract(e);
    internal TriageResult TriageStage(EmailExtraction x, IncomingEmail e) => Triage(x, e);
    internal IntentDecision IntentStage(EmailExtraction x, TriageResult t) => DecideIntent(x, t);
    internal TaskExecution TaskStage(IncomingEmail e, EmailExtraction x, TriageResult t, IntentDecision i) => RunTask(e, x, t, i);
    internal OutcomeReport OutcomeStage(IncomingEmail e, EmailExtraction x, TriageResult t, IntentDecision i, TaskExecution k) => BuildOutcome(e, x, t, i, k);
    internal double Threshold => _options.IntentHumanReviewThreshold;

    internal string StatusFor(TriageResult t, IntentDecision i) =>
        t.Category == "Spam" ? "closed-spam" : (i.RequiresHuman ? "awaiting-human" : "completed");

    internal void EnqueueHumanIfNeeded(EmailCase c)
    {
        if (!c.Intent.RequiresHuman) return;
        _queue.Enqueue(new HumanReviewItem
        {
            CaseId = c.CaseId,
            Reference = c.Reference,
            Subject = c.Source.Subject,
            FromName = c.Source.FromName,
            ProposedIntent = c.Intent.Intent,
            IntentConfidence = c.Intent.IntentConfidence,
            Reason = c.Intent.HumanReason,
            Queue = c.Intent.SuggestedQueue
        });
    }

    private static T Step<T>(List<AgentStep> trace, string stage, string agent,
        Func<T, string> summarise, Func<T, string> decide, Func<T, double?> conf, Func<T> work)
    {
        var sw = Stopwatch.StartNew();
        var result = work();
        sw.Stop();
        trace.Add(new AgentStep
        {
            Stage = stage,
            Agent = agent,
            Engine = "offline",
            Summary = summarise(result),
            Decision = decide(result),
            Confidence = conf(result),
            DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds),
            At = DateTimeOffset.UtcNow
        });
        return result;
    }
}
