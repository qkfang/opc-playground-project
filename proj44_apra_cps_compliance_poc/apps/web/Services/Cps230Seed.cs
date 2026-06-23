using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

/// <summary>
/// Deterministic, code-defined seed of the APRA CPS 230 (Operational Risk Management) compliance
/// framework. This is the GROUND TRUTH that guarantees the required data scale (≥130 policies,
/// ≥30 controls) with a fully mapped Requirement → Policy → Standard → Control spine and a small,
/// deliberate set of gaps so the Gap Analysis tab has real findings.
///
/// The model never needs to "hit" these counts — the offline engine emits this exact graph, and the
/// Foundry multi-agent path falls back to it on any failure (mirroring proj37). Counts are asserted
/// in tests and smoke, so they must remain stable here.
///
/// CPS 230 theme taxonomy (substantively aligned to the prudential standard):
///   governance   — Board accountability &amp; operational risk governance
///   framework    — Operational risk management framework (identify/assess/manage/monitor)
///   controls     — Operational risk controls &amp; control testing/monitoring
///   resilience   — Business &amp; operational resilience, tolerance levels
///   critical     — Critical operations identification &amp; management
///   serviceprov  — Service provider / material arrangement management
///   incident     — Incident &amp; disruption management
///   bcp          — Business continuity planning
///   testing      — Scenario / severe-but-plausible testing
///   notification — Notifications to APRA
/// </summary>
public static class Cps230Seed
{
    public sealed record Theme(string Key, string Name, string PolicyDomain);

    public static readonly IReadOnlyList<Theme> Themes = new[]
    {
        new Theme("governance",   "Board Accountability & Governance",            "Governance & Accountability"),
        new Theme("framework",    "Operational Risk Management Framework",        "Operational Risk Management"),
        new Theme("controls",     "Operational Risk Controls & Monitoring",       "Risk Controls & Assurance"),
        new Theme("resilience",   "Business & Operational Resilience",            "Operational Resilience"),
        new Theme("critical",     "Critical Operations",                          "Critical Operations"),
        new Theme("serviceprov",  "Service Provider Management",                  "Third-Party & Service Provider Risk"),
        new Theme("incident",     "Incident & Disruption Management",             "Incident Management"),
        new Theme("bcp",          "Business Continuity Planning",                 "Business Continuity"),
        new Theme("testing",      "Scenario & Severe-but-Plausible Testing",      "Resilience Testing"),
        new Theme("notification", "APRA Notifications",                           "Regulatory Engagement"),
    };

    /// <summary>The source-standard metadata (Ingestion agent surfaces this on the Overview tab).</summary>
    public static RegulatorySource BuildSource() => new()
    {
        Regulator = "APRA",
        Code = "CPS 230",
        Title = "Operational Risk Management",
        Version = "Effective 1 July 2025 (Prudential Standard CPS 230)",
        Summary =
            "APRA Prudential Standard CPS 230 requires an APRA-regulated entity to effectively manage its " +
            "operational risks, maintain critical operations within Board-approved tolerance levels through " +
            "severe disruptions, and manage the risks arising from the use of service providers. It consolidates " +
            "and replaces several standards (including CPS 231 Outsourcing and CPS 232 Business Continuity " +
            "Management) and sets requirements across governance, operational risk management, business " +
            "continuity and service provider management.",
        Themes = Themes.Select(t => t.Name).ToList()
    };

    /// <summary>
    /// Parsed clauses (Ingestion agent output). One representative clause per theme plus a few key
    /// sub-clauses, so the Overview/Ingestion tab shows a real document breakdown.
    /// </summary>
    public static List<RegulatoryClause> BuildClauses()
    {
        var c = new List<RegulatoryClause>();
        int n = 1;
        void Add(string theme, string reference, string heading, string text) =>
            c.Add(new RegulatoryClause { Id = $"CL-{n++:00}", Theme = theme, Reference = reference, Heading = heading, Text = text });

        Add("governance", "Paragraphs 13-18", "Roles and responsibilities",
            "The Board is ultimately accountable for the oversight of operational risk management. The entity must clearly define the roles and responsibilities of the Board, senior management and risk and control functions for operational risk, business continuity and the management of service providers.");
        Add("framework", "Paragraphs 19-26", "Operational risk management",
            "An entity must maintain an operational risk management framework, must identify and assess operational risks, must monitor and report on its operational risk profile, and must take action to remediate operational risk control weaknesses in a timely manner.");
        Add("controls", "Paragraphs 27-32", "Operational risk controls",
            "An entity must design, implement and embed internal controls to manage its operational risks, and must regularly assess the design and operating effectiveness of those controls, including controls operated by service providers.");
        Add("resilience", "Paragraphs 33-40", "Business continuity and tolerance levels",
            "An entity must maintain the ability to continue its critical operations within tolerance levels through severe disruptions. The Board must approve tolerance levels for each critical operation expressed as the maximum level of disruption the entity is willing to accept.");
        Add("critical", "Paragraphs 35-37", "Critical operations",
            "An entity must identify its critical operations and maintain a register. Critical operations are processes which, if disrupted beyond tolerance, would have a material adverse impact on depositors, policyholders, beneficiaries or the financial system.");
        Add("serviceprov", "Paragraphs 41-55", "Management of service providers",
            "An entity must maintain a service provider management policy, maintain a register of material service providers, conduct due diligence and ongoing monitoring, and ensure formal agreements are in place for material arrangements. APRA must be notified of material arrangements and of material changes.");
        Add("incident", "Paragraphs 31-32", "Incident management",
            "An entity must be able to identify, escalate, record, respond to and learn from operational risk incidents, and must manage disruptions to critical operations to restore them within tolerance.");
        Add("bcp", "Paragraphs 38-39", "Business continuity plan",
            "An entity must maintain a business continuity plan (BCP) that sets out how it will maintain critical operations within tolerance through disruptions, including the resources, actions and arrangements required.");
        Add("testing", "Paragraph 40", "Scenario testing",
            "An entity must conduct scenario testing of its ability to maintain critical operations within tolerance under a range of severe but plausible scenarios, and must review its BCP at least annually.");
        Add("notification", "Paragraphs 56-58", "Notification requirements",
            "An entity must notify APRA as soon as possible and no later than 72 hours after becoming aware of an operational risk incident likely to have a material financial impact or a material impact on the ability to maintain critical operations, and must notify APRA prior to entering into or materially changing a material service provider arrangement.");
        return c;
    }
}
