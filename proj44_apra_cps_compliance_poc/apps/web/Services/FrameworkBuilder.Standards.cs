using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

public static partial class FrameworkBuilder
{
    // =====================================================================================
    // STANDARDS  (40)  -- implementation standards ("how") grouped by domain.
    // STD-039 and STD-040 are deliberately left without a mapped control (known gaps).
    // =====================================================================================
    private static List<ImplementationStandard> BuildStandards()
    {
        var list = new List<ImplementationStandard>();
        int n = 1;
        void Add(string theme, string title, string requirement)
        {
            var domain = Cps230Seed.Themes.First(t => t.Key == theme).PolicyDomain;
            list.Add(new ImplementationStandard { Id = $"STD-{n++:000}", Domain = domain, Title = title, Requirement = requirement });
        }

        // governance (4)
        Add("governance", "Operational risk governance standard", "Operational risk governance forums, membership, cadence and reporting lines are documented and operated.");
        Add("governance", "Accountability mapping standard", "Accountable persons are mapped to operational risk, resilience and service-provider obligations and kept current.");
        Add("governance", "Policy lifecycle standard", "Policies follow a defined drafting, approval, publication and annual-review lifecycle.");
        Add("governance", "Risk appetite calibration standard", "Operational risk appetite and tolerance metrics are calibrated, approved and monitored.");

        // framework (5)
        Add("framework", "RCSA execution standard", "Risk and control self-assessments are scoped, scheduled, executed and challenged on a defined cycle.");
        Add("framework", "Operational risk taxonomy standard", "A standard taxonomy classifies risks, controls, incidents and losses consistently.");
        Add("framework", "KRI standard", "Key risk indicators are defined with thresholds, owners, data sources and escalation.");
        Add("framework", "Change risk assessment standard", "Material change and new-product processes include a mandatory operational risk assessment gate.");
        Add("framework", "Loss data capture standard", "Operational loss and near-miss events are captured against thresholds with mandatory fields.");

        // controls (6)
        Add("controls", "Control design standard", "Controls are documented with objective, type, frequency, owner and evidence requirements.");
        Add("controls", "Control testing standard", "Key control testing uses risk-based sampling, defined pass/fail criteria and re-test on failure.");
        Add("controls", "Control monitoring standard", "Control performance is monitored continuously where feasible and exceptions are escalated.");
        Add("controls", "Segregation of duties standard", "Toxic-combination access and approval conflicts are prevented and periodically reviewed.");
        Add("controls", "Access management standard", "Access to critical systems follows least-privilege, joiner-mover-leaver and periodic recertification.");
        Add("controls", "Service-provider assurance standard", "Independent assurance reports (e.g. SOC) over service providers are obtained and reviewed.");

        // resilience (5)
        Add("resilience", "Tolerance-level standard", "Tolerance levels are expressed as measurable maximum disruption and approved by the Board.");
        Add("resilience", "Resource-mapping standard", "Resources supporting critical operations are mapped, with dependencies and single points of failure.");
        Add("resilience", "Recovery-objective standard", "Recovery time and recovery point objectives are set consistent with tolerance levels.");
        Add("resilience", "Interdependency-mapping standard", "Internal and external interdependencies for critical operations are mapped and reviewed.");
        Add("resilience", "Resilience-reporting standard", "Resilience posture, breaches and remediation are reported to governance on a defined cadence.");

        // critical (4)
        Add("critical", "Critical-operations identification standard", "Critical operations are identified using defined impact criteria and approved by governance.");
        Add("critical", "Critical-operations register standard", "The critical operations register records owner, tolerance, resources and dependencies.");
        Add("critical", "Critical-operations review standard", "The register is reviewed at least annually and upon material change.");
        Add("critical", "Critical-data protection standard", "Data critical to critical operations is classified, protected and recoverable.");

        // serviceprov (6)
        Add("serviceprov", "Materiality assessment standard", "A defined methodology assesses whether a service-provider arrangement is material.");
        Add("serviceprov", "Due-diligence standard", "Pre-contract due diligence covers financial, operational, security and resilience risk.");
        Add("serviceprov", "Contractual-requirements standard", "Material-arrangement contracts include audit, access, sub-contracting, exit and notification clauses.");
        Add("serviceprov", "Ongoing-monitoring standard", "Material service providers are monitored against SLAs, risk indicators and assurance evidence.");
        Add("serviceprov", "Concentration-risk standard", "Service-provider concentration and substitutability are assessed at portfolio level.");
        Add("serviceprov", "Service-provider exit standard", "Exit and transition plans for material arrangements are documented and tested where feasible.");

        // incident (3)
        Add("incident", "Incident-management standard", "Incidents are logged, classified, escalated and resolved under defined timeframes.");
        Add("incident", "Root-cause-analysis standard", "Material incidents undergo structured root-cause analysis with remediation tracking.");
        Add("incident", "Crisis-management standard", "Crisis management activation, roles and communications are defined and exercised.");

        // bcp (3)
        Add("bcp", "BCP content standard", "Business continuity plans contain strategies, resources, roles and recovery steps per critical operation.");
        Add("bcp", "IT disaster-recovery standard", "IT disaster recovery plans align to recovery objectives and are version-controlled.");
        Add("bcp", "Workaround-procedure standard", "Manual workaround procedures are documented for prolonged system outages.");

        // testing (2)  -- STD-037 and STD-038 are deliberately left WITHOUT a control (known gaps)
        Add("testing", "Scenario-testing standard", "Scenario tests use severe-but-plausible scenarios with defined success criteria and reporting.");
        Add("testing", "Test-remediation standard", "Deficiencies from testing are logged, owned, remediated and re-tested within agreed timeframes.");

        return list;
    }
}
