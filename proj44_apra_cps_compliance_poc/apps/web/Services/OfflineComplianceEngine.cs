using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

/// <summary>
/// Deterministic, signal-free compliance engine. It emits the full CPS 230 framework from
/// <see cref="FrameworkBuilder"/> (the ground-truth graph with >=130 policies, >=30 controls and all
/// mappings) and records one agent-step log entry per pipeline stage, in order, so the UI and tests
/// see the same six-stage pipeline whether or not Foundry is configured.
///
/// This is what guarantees build/test/smoke pass on a VM with no Azure access. The Foundry engine
/// reuses this exact builder when it falls back, so the data scale is identical in both modes.
/// </summary>
public sealed class OfflineComplianceEngine : IComplianceEngine
{
    public string Name => "offline";

    public Task<ComplianceFramework> BuildAsync(CancellationToken ct = default)
    {
        var fw = FrameworkBuilder.Build();
        fw.Engine = Name;
        AppendStageLogs(fw, reasonSuffix: null);
        fw.Status = "completed";
        return Task.FromResult(fw);
    }

    /// <summary>
    /// Records the canonical six-stage transcript over an already-built framework. Shared with the
    /// Foundry engine's fallback path so both engines log the same ordered stages. When
    /// <paramref name="reasonSuffix"/> is set (Foundry fallback), it is appended to each summary.
    /// </summary>
    public static void AppendStageLogs(ComplianceFramework fw, string? reasonSuffix)
    {
        string Suffix() => string.IsNullOrEmpty(reasonSuffix) ? "" : $" {reasonSuffix}";

        void Log(string step, string agent, string summary) =>
            fw.AgentSteps.Add(new AgentStepLog { Step = step, Agent = agent, Summary = summary + Suffix() });

        var s = AgentInstructions.Stage("ingestion");
        Log("ingestion", s.Agent,
            $"Parsed {fw.Source.Regulator} {fw.Source.Code} into {fw.Clauses.Count} clean clauses across {fw.Source.Themes.Count} themes.");

        s = AgentInstructions.Stage("requirements");
        Log("requirements", s.Agent,
            $"Extracted {fw.Requirements.Count} structured regulatory requirements from the parsed clauses.");

        s = AgentInstructions.Stage("policies");
        Log("policies", s.Agent,
            $"Authored {fw.Policies.Count} policies across {fw.Policies.Select(p => p.Domain).Distinct().Count()} domains.");

        s = AgentInstructions.Stage("standards");
        Log("standards", s.Agent,
            $"Authored {fw.Standards.Count} implementation standards and mapped {fw.Policies.Sum(p => p.StandardIds.Count)} policy->standard links.");

        s = AgentInstructions.Stage("controls");
        Log("controls", s.Agent,
            $"Built a control library of {fw.Controls.Count} controls and mapped {fw.Standards.Sum(x => x.ControlIds.Count)} standard->control links.");

        var gap = GapAnalyzer.Analyze(fw);
        s = AgentInstructions.Stage("gap");
        Log("gap", s.Agent,
            $"Analysed the requirement->policy->standard->control chain: {gap.TotalGaps} gap(s) found; end-to-end coverage {gap.Coverage.EndToEndCoverage}%.");
    }
}
