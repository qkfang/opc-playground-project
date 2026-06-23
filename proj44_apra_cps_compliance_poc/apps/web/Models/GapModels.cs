namespace Proj44.Compliance.Web.Models;

/// <summary>
/// Result of the Gap/traceability analysis agent: orphaned items at every layer of the
/// Requirement → Policy → Standard → Control spine, plus coverage percentages. This is what the
/// Gap Analysis tab and GET /api/gaps render.
///
/// An item is an "orphan" when it has no outbound link to the next layer down
/// (requirement with no policy, policy with no standard, standard with no control). Coverage is the
/// proportion of items at a layer that DO have at least one such link.
/// </summary>
public sealed class GapAnalysis
{
    public DateTimeOffset GeneratedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<GapItem> UnmappedRequirements { get; set; } = new();   // requirement → policy missing
    public List<GapItem> UnmappedPolicies { get; set; } = new();       // policy → standard missing
    public List<GapItem> UnmappedStandards { get; set; } = new();      // standard → control missing

    /// <summary>Controls that no standard references (dangling control library entries).</summary>
    public List<GapItem> OrphanControls { get; set; } = new();

    public CoverageStats Coverage { get; set; } = new();

    /// <summary>Plain-English findings the UI lists under the Gap Analysis tab.</summary>
    public List<string> Findings { get; set; } = new();

    public int TotalGaps =>
        UnmappedRequirements.Count + UnmappedPolicies.Count + UnmappedStandards.Count + OrphanControls.Count;
}

public sealed class GapItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Layer { get; set; } = "";     // Requirement | Policy | Standard | Control
    public string Detail { get; set; } = "";    // why it's a gap
}

public sealed class CoverageStats
{
    /// <summary>% of requirements with >=1 mapped policy.</summary>
    public double RequirementCoverage { get; set; }
    /// <summary>% of policies with >=1 mapped standard.</summary>
    public double PolicyCoverage { get; set; }
    /// <summary>% of standards with >=1 mapped control.</summary>
    public double StandardCoverage { get; set; }
    /// <summary>% of controls referenced by >=1 standard.</summary>
    public double ControlCoverage { get; set; }

    /// <summary>End-to-end coverage: % of requirements that trace all the way to >=1 control.</summary>
    public double EndToEndCoverage { get; set; }
}

/// <summary>
/// A fully resolved traceability chain for a single requirement (GET /api/traceability/{id}).
/// Walks requirement → policies → standards → controls and reports whether the chain is complete.
/// </summary>
public sealed class TraceabilityChain
{
    public RegulatoryRequirement Requirement { get; set; } = new();
    public List<TraceabilityPolicyNode> Policies { get; set; } = new();

    /// <summary>True when at least one policy → standard → control path reaches a control.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Where the chain breaks, if incomplete (e.g. "no policy", "policy POL-12 has no standard").</summary>
    public List<string> BrokenLinks { get; set; } = new();
}

public sealed class TraceabilityPolicyNode
{
    public Policy Policy { get; set; } = new();
    public List<TraceabilityStandardNode> Standards { get; set; } = new();
}

public sealed class TraceabilityStandardNode
{
    public ImplementationStandard Standard { get; set; } = new();
    public List<Control> Controls { get; set; } = new();
}
