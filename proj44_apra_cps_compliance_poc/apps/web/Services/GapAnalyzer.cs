using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

/// <summary>
/// Computes the Gap/traceability analysis over a built framework: orphaned items at each layer of the
/// Requirement -> Policy -> Standard -> Control spine, plus coverage percentages and plain findings.
/// Pure and deterministic so the Gap Analysis tab, GET /api/gaps and the tests all agree.
/// </summary>
public static class GapAnalyzer
{
    public static GapAnalysis Analyze(ComplianceFramework fw)
    {
        var ga = new GapAnalysis();

        // Requirements with no mapped policy.
        foreach (var r in fw.Requirements.Where(r => r.PolicyIds.Count == 0))
            ga.UnmappedRequirements.Add(new GapItem
            {
                Id = r.Id, Title = r.Title, Layer = "Requirement",
                Detail = "No policy maps to this regulatory requirement."
            });

        // Policies with no mapped standard.
        foreach (var p in fw.Policies.Where(p => p.StandardIds.Count == 0))
            ga.UnmappedPolicies.Add(new GapItem
            {
                Id = p.Id, Title = p.Title, Layer = "Policy",
                Detail = "Policy is not operationalised by any implementation standard."
            });

        // Standards with no mapped control.
        foreach (var s in fw.Standards.Where(s => s.ControlIds.Count == 0))
            ga.UnmappedStandards.Add(new GapItem
            {
                Id = s.Id, Title = s.Title, Layer = "Standard",
                Detail = "Standard has no control enforcing it (untested in the control library)."
            });

        // Controls referenced by no standard.
        var referenced = new HashSet<string>(fw.Standards.SelectMany(s => s.ControlIds));
        foreach (var c in fw.Controls.Where(c => !referenced.Contains(c.Id)))
            ga.OrphanControls.Add(new GapItem
            {
                Id = c.Id, Title = c.Title, Layer = "Control",
                Detail = "Control exists in the library but no standard references it."
            });

        ga.Coverage = ComputeCoverage(fw);
        ga.Findings = BuildFindings(fw, ga);
        return ga;
    }

    private static CoverageStats ComputeCoverage(ComplianceFramework fw)
    {
        double Pct(int num, int den) => den == 0 ? 100.0 : Math.Round(100.0 * num / den, 1);

        var reqCovered = fw.Requirements.Count(r => r.PolicyIds.Count > 0);
        var polCovered = fw.Policies.Count(p => p.StandardIds.Count > 0);
        var stdCovered = fw.Standards.Count(s => s.ControlIds.Count > 0);
        var referenced = new HashSet<string>(fw.Standards.SelectMany(s => s.ControlIds));
        var ctlCovered = fw.Controls.Count(c => referenced.Contains(c.Id));

        // End-to-end: a requirement traces fully when at least one of its policies reaches a standard
        // that reaches a control.
        var policyById = fw.Policies.ToDictionary(p => p.Id);
        var standardById = fw.Standards.ToDictionary(s => s.Id);
        int e2e = fw.Requirements.Count(r => r.PolicyIds
            .Select(pid => policyById.GetValueOrDefault(pid))
            .Where(p => p is not null)
            .Any(p => p!.StandardIds
                .Select(sid => standardById.GetValueOrDefault(sid))
                .Where(s => s is not null)
                .Any(s => s!.ControlIds.Count > 0)));

        return new CoverageStats
        {
            RequirementCoverage = Pct(reqCovered, fw.Requirements.Count),
            PolicyCoverage = Pct(polCovered, fw.Policies.Count),
            StandardCoverage = Pct(stdCovered, fw.Standards.Count),
            ControlCoverage = Pct(ctlCovered, fw.Controls.Count),
            EndToEndCoverage = Pct(e2e, fw.Requirements.Count)
        };
    }

    private static List<string> BuildFindings(ComplianceFramework fw, GapAnalysis ga)
    {
        var f = new List<string>();
        if (ga.UnmappedRequirements.Count > 0)
            f.Add($"{ga.UnmappedRequirements.Count} regulatory requirement(s) have no mapped policy: {string.Join(", ", ga.UnmappedRequirements.Select(x => x.Id))}.");
        if (ga.UnmappedPolicies.Count > 0)
            f.Add($"{ga.UnmappedPolicies.Count} policy/policies are not operationalised by any standard: {string.Join(", ", ga.UnmappedPolicies.Select(x => x.Id))}.");
        if (ga.UnmappedStandards.Count > 0)
            f.Add($"{ga.UnmappedStandards.Count} standard(s) have no enforcing control: {string.Join(", ", ga.UnmappedStandards.Select(x => x.Id))}.");
        if (ga.OrphanControls.Count > 0)
            f.Add($"{ga.OrphanControls.Count} control(s) are not referenced by any standard: {string.Join(", ", ga.OrphanControls.Select(x => x.Id))}.");
        if (ga.TotalGaps == 0)
            f.Add("No gaps detected: every layer of the Requirement -> Policy -> Standard -> Control spine is fully mapped.");

        f.Add($"Coverage - requirements {ga.Coverage.RequirementCoverage}%, policies {ga.Coverage.PolicyCoverage}%, " +
              $"standards {ga.Coverage.StandardCoverage}%, controls {ga.Coverage.ControlCoverage}%, end-to-end {ga.Coverage.EndToEndCoverage}%.");
        return f;
    }
}
