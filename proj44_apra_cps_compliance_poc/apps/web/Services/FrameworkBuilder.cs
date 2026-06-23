using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

/// <summary>
/// Builds the complete, deterministic APRA CPS 230 compliance-mapping framework: the curated
/// requirement set, a policy library of >=130 items, an implementation-standard layer, a control
/// library of >=30 controls, and every Requirement -> Policy -> Standard -> Control mapping, plus a
/// small, intentional set of gaps at each layer so the Gap Analysis tab surfaces real findings.
///
/// This is the single source of truth for the framework graph. Both engines depend on it:
///   * OfflineComplianceEngine returns this graph verbatim (deterministic; no Azure needed).
///   * FoundryComplianceEngine orchestrates the six stage-agents, but if ANY stage fails it falls
///     back to this builder so the POC always produces a complete, correctly-scaled framework.
///
/// Counts are deliberately stable (asserted by tests + smoke). The policy/standard/control catalogues
/// are realistic CPS 230 content grouped by theme.
/// </summary>
public static partial class FrameworkBuilder
{
    /// <summary>IDs deliberately left without a downstream mapping (the known gaps for Gap Analysis).</summary>
    public static class KnownGaps
    {
        // Requirements with NO mapped policy.
        public static readonly string[] RequirementsWithoutPolicy = { "REQ-034", "REQ-035" };
        // Policies with NO mapped standard.
        public static readonly string[] PoliciesWithoutStandard = { "POL-128", "POL-129", "POL-130" };
        // Standards with NO mapped control (these are the last two testing standards in the catalogue).
        public static readonly string[] StandardsWithoutControl = { "STD-037", "STD-038" };
    }

    public static ComplianceFramework Build()
    {
        var fw = new ComplianceFramework
        {
            Engine = "offline",
            Source = Cps230Seed.BuildSource(),
            Clauses = Cps230Seed.BuildClauses()
        };

        fw.Requirements = BuildRequirements();
        fw.Policies = BuildPolicies();
        fw.Standards = BuildStandards();
        fw.Controls = BuildControls();

        WireMappings(fw);
        return fw;
    }
}
