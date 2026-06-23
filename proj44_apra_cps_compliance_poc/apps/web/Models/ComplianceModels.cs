using System.Text.Json.Serialization;

namespace Proj44.Compliance.Web.Models;

/// <summary>
/// The full compliance-mapping graph produced by the pipeline: the source regulatory standard
/// (APRA CPS 230), the extracted requirements, and the authored Policy → Standard → Control layers
/// with every cross-layer mapping. This single aggregate is what every API endpoint and UI tab reads.
///
/// Layering (regulatory traceability spine):
///   Requirement (what the regulation demands)
///     → Policy        (the firm's governing intent)
///       → Standard    (the implementation rule / "how")
///         → Control   (the operating mechanism that is tested for effectiveness)
/// </summary>
public sealed class ComplianceFramework
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "completed";

    /// <summary>"foundry" when the live multi-agent pipeline ran, otherwise "offline".</summary>
    public string Engine { get; set; } = "offline";

    /// <summary>The regulatory corpus this framework maps (APRA CPS 230).</summary>
    public RegulatorySource Source { get; set; } = new();

    /// <summary>Clean sections/clauses parsed from the CPS document (Ingestion agent output).</summary>
    public List<RegulatoryClause> Clauses { get; set; } = new();

    public List<RegulatoryRequirement> Requirements { get; set; } = new();
    public List<Policy> Policies { get; set; } = new();
    public List<ImplementationStandard> Standards { get; set; } = new();
    public List<Control> Controls { get; set; } = new();

    /// <summary>Per-stage agent transcript (one entry per stage) for transparency / audit.</summary>
    public List<AgentStepLog> AgentSteps { get; set; } = new();

    // ---- Derived counts (used by /api/framework, tests, smoke, UI badges) ----
    public FrameworkCounts Counts => new()
    {
        Clauses = Clauses.Count,
        Requirements = Requirements.Count,
        Policies = Policies.Count,
        Standards = Standards.Count,
        Controls = Controls.Count,
        RequirementToPolicyLinks = Requirements.Sum(r => r.PolicyIds.Count),
        PolicyToStandardLinks = Policies.Sum(p => p.StandardIds.Count),
        StandardToControlLinks = Standards.Sum(s => s.ControlIds.Count)
    };
}

/// <summary>Metadata describing the source regulatory standard being mapped.</summary>
public sealed class RegulatorySource
{
    public string Regulator { get; set; } = "APRA";
    public string Code { get; set; } = "CPS 230";
    public string Title { get; set; } = "Operational Risk Management";
    public string Version { get; set; } = "Effective 1 July 2025";
    public string Summary { get; set; } = "";
    public List<string> Themes { get; set; } = new();
}

/// <summary>A parsed section/clause of the CPS document (Ingestion agent output).</summary>
public sealed class RegulatoryClause
{
    public string Id { get; set; } = "";            // CL-01
    public string Reference { get; set; } = "";     // "Paragraph 21" / "Attachment A"
    public string Theme { get; set; } = "";         // governance | resilience | ...
    public string Heading { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>A structured regulatory requirement extracted from the CPS (Requirement agent output).</summary>
public sealed class RegulatoryRequirement
{
    public string Id { get; set; } = "";            // REQ-001
    public string ClauseId { get; set; } = "";      // CL-03 (provenance)
    public string Theme { get; set; } = "";         // governance | framework | resilience | ...
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";          // paraphrased obligation
    public string Obligation { get; set; } = "Must"; // Must | Should
    public List<string> PolicyIds { get; set; } = new();
}

/// <summary>A firm policy statement (Policy authoring agent output).</summary>
public sealed class Policy
{
    public string Id { get; set; } = "";            // POL-001
    public string Domain { get; set; } = "";        // Governance & Accountability | Operational Risk | ...
    public string Title { get; set; } = "";
    public string Statement { get; set; } = "";
    public string Owner { get; set; } = "";         // accountable function
    public List<string> StandardIds { get; set; } = new();
}

/// <summary>An implementation standard that operationalises one or more policies (Standard agent output).</summary>
public sealed class ImplementationStandard
{
    public string Id { get; set; } = "";            // STD-001
    public string Domain { get; set; } = "";
    public string Title { get; set; } = "";
    public string Requirement { get; set; } = "";   // the "how" rule
    public List<string> ControlIds { get; set; } = new();
}

/// <summary>A control in the control library that enforces one or more standards (Control agent output).</summary>
public sealed class Control
{
    public string Id { get; set; } = "";            // CTL-001
    public string Domain { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "Preventive"; // Preventive | Detective | Corrective | Directive
    public string Frequency { get; set; } = "Continuous"; // Continuous | Daily | Monthly | Quarterly | Annual | Event-driven
    public string TestMethod { get; set; } = "";
}

public sealed class AgentStepLog
{
    public string Step { get; set; } = "";          // ingestion | requirements | policies | standards | controls | gap
    public string Agent { get; set; } = "";         // human-readable agent name (persona)
    public string Summary { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FrameworkCounts
{
    public int Clauses { get; set; }
    public int Requirements { get; set; }
    public int Policies { get; set; }
    public int Standards { get; set; }
    public int Controls { get; set; }
    public int RequirementToPolicyLinks { get; set; }
    public int PolicyToStandardLinks { get; set; }
    public int StandardToControlLinks { get; set; }
}
