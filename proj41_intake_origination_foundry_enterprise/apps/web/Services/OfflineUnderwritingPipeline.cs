using System.Diagnostics;
using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services;

/// <summary>
/// Deterministic, offline implementation of the underwriting origination pipeline.
/// Uses heuristic NLP over the submission text so the POC always runs with no Azure/Foundry
/// connectivity. The Foundry pipeline reuses these heuristics as a per-stage fallback.
///
/// Stages:
///   1. Submission Intake Agent  -> ExtractedRecords (Producer/Insured/RiskSubmission)
///   2. Appetite & Triage Agent  -> AppetiteDecision (appetite class, risk/fit score, routing)
///   3. Risk Research Agent       -> LeadResearch (exposure + demand signals)
///   4. Underwriting Study Agent  -> UnderwritingStudy (executive risk study)
/// </summary>
public sealed partial class OfflineUnderwritingPipeline : IUnderwritingPipeline
{
    public string Name => "offline";

    private static int _seq = 41_000;

    public Task<SubmissionCase> RunAsync(SubmissionEmail email, CancellationToken ct = default)
    {
        var trace = new List<AgentTrace>();

        var records = Time(trace, "Submission Intake", "intake-agent",
            "Parsed producer, insured and risk submission from the broker email.",
            () => ExtractRecords(email));

        var triage = Time(trace, "Appetite & Triage", "triage-agent",
            "Scored appetite/risk and routed to an underwriting desk.",
            () => Triage(records));

        var research = Time(trace, "Risk Research", "research-agent",
            "Captured exposure and inbound demand signals for the insured.",
            () => Research(email, records, triage));

        var study = Time(trace, "Underwriting Study", "study-agent",
            "Generated the executive underwriting risk study.",
            () => BuildStudy(records, triage, research));

        var reference = $"SUB-{DateTimeOffset.UtcNow:yyyy}-{Interlocked.Increment(ref _seq):D5}";

        var c = new SubmissionCase
        {
            Reference = reference,
            Status = triage.Declined ? "declined" : "completed",
            Engine = Name,
            Source = email,
            Records = records,
            Triage = triage,
            Research = research,
            Study = study,
            Trace = trace
        };
        return Task.FromResult(c);
    }

    /// <summary>The offline pipeline is, by definition, not the live Foundry path.</summary>
    public Task<EngineDiagnostics> ProbeAsync(CancellationToken ct = default) =>
        Task.FromResult(new EngineDiagnostics
        {
            FoundryMode = "offline",
            FoundryLive = false,
            FoundryConfigured = false,
            FoundryEnabled = false,
            Detail = "Foundry not configured; deterministic offline engine is active by design."
        });

    // ---- Internal per-stage accessors so the Foundry pipeline can fall back stage-by-stage. ----
    internal ExtractedRecords ExtractStage(SubmissionEmail email) => ExtractRecords(email);
    internal AppetiteDecision TriageStage(ExtractedRecords r) => Triage(r);
    internal LeadResearch ResearchStage(SubmissionEmail email, ExtractedRecords r, AppetiteDecision t) => Research(email, r, t);
    internal UnderwritingStudy StudyStage(ExtractedRecords r, AppetiteDecision t, LeadResearch res) => BuildStudy(r, t, res);

    private static T Time<T>(List<AgentTrace> trace, string stage, string agent, string summary, Func<T> work)
    {
        var sw = Stopwatch.StartNew();
        var result = work();
        sw.Stop();
        trace.Add(new AgentTrace
        {
            Stage = stage,
            Agent = agent,
            Engine = "offline",
            Summary = summary,
            DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds),
            At = DateTimeOffset.UtcNow
        });
        return result;
    }
}
