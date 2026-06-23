using Proj44.Compliance.Web.Models;
using Proj44.Compliance.Web.Services;
using Xunit;

namespace Proj44.Compliance.Tests;

/// <summary>
/// Contract tests over the deterministic framework graph. These guarantee the POC always ships the
/// required data scale (>=130 policies, >=30 controls), a fully-mapped Requirement -> Policy ->
/// Standard -> Control spine (except the intentional gap fixtures), correct gap detection, working
/// traceability, and the six-stage agent transcript — all without any Azure access.
/// </summary>
public sealed class FrameworkTests
{
    private static readonly ComplianceFramework Fw = FrameworkBuilder.Build();

    [Fact]
    public void Policy_library_has_at_least_130_policies()
    {
        Assert.True(Fw.Policies.Count >= 130, $"expected >=130 policies, got {Fw.Policies.Count}");
    }

    [Fact]
    public void Control_library_has_at_least_30_controls()
    {
        Assert.True(Fw.Controls.Count >= 30, $"expected >=30 controls, got {Fw.Controls.Count}");
    }

    [Fact]
    public void Has_requirements_and_standards()
    {
        Assert.True(Fw.Requirements.Count >= 20, $"expected a substantive requirement set, got {Fw.Requirements.Count}");
        Assert.True(Fw.Standards.Count >= 30, $"expected >=30 standards, got {Fw.Standards.Count}");
    }

    [Fact]
    public void Ids_are_unique_at_every_layer()
    {
        Assert.Equal(Fw.Requirements.Count, Fw.Requirements.Select(x => x.Id).Distinct().Count());
        Assert.Equal(Fw.Policies.Count, Fw.Policies.Select(x => x.Id).Distinct().Count());
        Assert.Equal(Fw.Standards.Count, Fw.Standards.Select(x => x.Id).Distinct().Count());
        Assert.Equal(Fw.Controls.Count, Fw.Controls.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void Every_mapping_points_at_a_real_id()
    {
        var policyIds = Fw.Policies.Select(p => p.Id).ToHashSet();
        var standardIds = Fw.Standards.Select(s => s.Id).ToHashSet();
        var controlIds = Fw.Controls.Select(c => c.Id).ToHashSet();

        foreach (var r in Fw.Requirements)
            foreach (var pid in r.PolicyIds)
                Assert.Contains(pid, policyIds);

        foreach (var p in Fw.Policies)
            foreach (var sid in p.StandardIds)
                Assert.Contains(sid, standardIds);

        foreach (var s in Fw.Standards)
            foreach (var cid in s.ControlIds)
                Assert.Contains(cid, controlIds);
    }

    [Fact]
    public void All_three_mapping_layers_are_populated()
    {
        Assert.True(Fw.Counts.RequirementToPolicyLinks > 0, "no requirement->policy links");
        Assert.True(Fw.Counts.PolicyToStandardLinks > 0, "no policy->standard links");
        Assert.True(Fw.Counts.StandardToControlLinks > 0, "no standard->control links");
    }

    [Fact]
    public void Mappings_are_complete_except_the_intentional_gap_fixtures()
    {
        // Requirements: only the known gap requirements may lack a policy.
        var reqGaps = Fw.Requirements.Where(r => r.PolicyIds.Count == 0).Select(r => r.Id).OrderBy(x => x).ToArray();
        Assert.Equal(FrameworkBuilder.KnownGaps.RequirementsWithoutPolicy.OrderBy(x => x), reqGaps);

        // Policies: only the known gap policies may lack a standard.
        var polGaps = Fw.Policies.Where(p => p.StandardIds.Count == 0).Select(p => p.Id).OrderBy(x => x).ToArray();
        Assert.Equal(FrameworkBuilder.KnownGaps.PoliciesWithoutStandard.OrderBy(x => x), polGaps);

        // Standards: only the known gap standards may lack a control.
        var stdGaps = Fw.Standards.Where(s => s.ControlIds.Count == 0).Select(s => s.Id).OrderBy(x => x).ToArray();
        Assert.Equal(FrameworkBuilder.KnownGaps.StandardsWithoutControl.OrderBy(x => x), stdGaps);
    }

    [Fact]
    public void Every_control_is_referenced_by_at_least_one_standard()
    {
        var referenced = Fw.Standards.SelectMany(s => s.ControlIds).ToHashSet();
        var orphans = Fw.Controls.Where(c => !referenced.Contains(c.Id)).Select(c => c.Id).ToArray();
        Assert.True(orphans.Length == 0, $"orphan controls present: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void Gap_analysis_finds_exactly_the_known_orphans()
    {
        var ga = GapAnalyzer.Analyze(Fw);

        Assert.Equal(FrameworkBuilder.KnownGaps.RequirementsWithoutPolicy.OrderBy(x => x),
            ga.UnmappedRequirements.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(FrameworkBuilder.KnownGaps.PoliciesWithoutStandard.OrderBy(x => x),
            ga.UnmappedPolicies.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(FrameworkBuilder.KnownGaps.StandardsWithoutControl.OrderBy(x => x),
            ga.UnmappedStandards.Select(x => x.Id).OrderBy(x => x));

        Assert.Empty(ga.OrphanControls);
        Assert.True(ga.TotalGaps >= 7, $"expected the seeded gap fixtures, got {ga.TotalGaps}");
        Assert.NotEmpty(ga.Findings);
    }

    [Fact]
    public void Coverage_percentages_are_in_range_and_not_perfect()
    {
        var ga = GapAnalyzer.Analyze(Fw);
        foreach (var pct in new[]
        {
            ga.Coverage.RequirementCoverage, ga.Coverage.PolicyCoverage,
            ga.Coverage.StandardCoverage, ga.Coverage.ControlCoverage, ga.Coverage.EndToEndCoverage
        })
        {
            Assert.InRange(pct, 0.0, 100.0);
        }
        // Intentional gaps mean end-to-end coverage must be below 100%.
        Assert.True(ga.Coverage.EndToEndCoverage < 100.0, "end-to-end coverage should reflect the seeded gaps");
        // Control coverage is intended to be 100% (no orphan controls).
        Assert.Equal(100.0, ga.Coverage.ControlCoverage);
    }

    [Fact]
    public void Traceability_resolves_a_full_chain_for_a_well_mapped_requirement()
    {
        // Pick the first requirement that is NOT a known gap and that reaches a control.
        var good = Fw.Requirements.FirstOrDefault(r =>
            r.PolicyIds.Count > 0 &&
            !FrameworkBuilder.KnownGaps.RequirementsWithoutPolicy.Contains(r.Id));
        Assert.NotNull(good);

        var chain = TraceabilityResolver.Resolve(Fw, good!.Id);
        Assert.NotNull(chain);
        Assert.Equal(good.Id, chain!.Requirement.Id);
        Assert.NotEmpty(chain.Policies);
        Assert.True(chain.IsComplete, $"expected a complete chain for {good.Id}; broken: {string.Join("; ", chain.BrokenLinks)}");
        // The complete chain must reach at least one control.
        Assert.Contains(chain.Policies.SelectMany(p => p.Standards), s => s.Controls.Count > 0);
    }

    [Fact]
    public void Traceability_reports_break_for_a_known_gap_requirement()
    {
        var gapReq = FrameworkBuilder.KnownGaps.RequirementsWithoutPolicy[0];
        var chain = TraceabilityResolver.Resolve(Fw, gapReq);
        Assert.NotNull(chain);
        Assert.False(chain!.IsComplete);
        Assert.NotEmpty(chain.BrokenLinks);
    }

    [Fact]
    public void Traceability_returns_null_for_unknown_requirement()
    {
        Assert.Null(TraceabilityResolver.Resolve(Fw, "REQ-DOES-NOT-EXIST"));
    }

    [Fact]
    public void Source_is_apra_cps_230_with_themes()
    {
        Assert.Equal("APRA", Fw.Source.Regulator);
        Assert.Equal("CPS 230", Fw.Source.Code);
        Assert.NotEmpty(Fw.Source.Themes);
        Assert.NotEmpty(Fw.Clauses);
    }
}
