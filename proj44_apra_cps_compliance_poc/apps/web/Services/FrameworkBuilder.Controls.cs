using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

public static partial class FrameworkBuilder
{
    // =====================================================================================
    // CONTROLS  (36)  -- control library entries that enforce the standards.
    // =====================================================================================
    private static List<Control> BuildControls()
    {
        var list = new List<Control>();
        int n = 1;
        void Add(string theme, string title, string description, string type, string frequency, string test)
        {
            var domain = Cps230Seed.Themes.First(t => t.Key == theme).PolicyDomain;
            list.Add(new Control
            {
                Id = $"CTL-{n++:000}", Domain = domain, Title = title, Description = description,
                Type = type, Frequency = frequency, TestMethod = test
            });
        }

        // governance
        Add("governance", "Operational risk committee oversight", "The operational risk committee meets per its charter and minutes record decisions and actions.", "Directive", "Monthly", "Inspect minutes and attendance for the period.");
        Add("governance", "Accountability statements maintained", "Accountability statements are current and reconciled to the obligations register.", "Preventive", "Quarterly", "Reconcile statements to the obligations register.");
        Add("governance", "Annual policy review attestation", "Policy owners attest to annual review and approval of operational risk policies.", "Detective", "Annual", "Verify attestations and approval records.");
        Add("governance", "Risk appetite breach escalation", "Operational risk appetite breaches are escalated to the Board risk committee.", "Detective", "Event-driven", "Trace sampled breaches to escalation records.");

        // framework
        Add("framework", "RCSA completion monitoring", "RCSAs are completed on schedule and overdue assessments are escalated.", "Detective", "Quarterly", "Compare completed RCSAs to the schedule.");
        Add("framework", "KRI threshold monitoring", "KRIs are produced and breaches trigger documented action.", "Detective", "Monthly", "Recompute sampled KRIs and verify breach actions.");
        Add("framework", "Change risk assessment gate", "Material changes cannot proceed without a completed operational risk assessment.", "Preventive", "Event-driven", "Test change records for a completed assessment gate.");
        Add("framework", "Loss event capture reconciliation", "Operational loss events are captured and reconciled to the general ledger.", "Detective", "Monthly", "Reconcile loss register to ledger postings.");
        Add("framework", "Issue and action ageing review", "Open operational risk issues are reviewed for ageing and breach of due dates.", "Detective", "Monthly", "Review ageing report and overdue escalations.");

        // controls
        Add("controls", "Key control testing execution", "Key controls are tested on the risk-based plan with results recorded.", "Detective", "Quarterly", "Re-perform a sample of key control tests.");
        Add("controls", "Control failure remediation tracking", "Failed controls have remediation plans tracked to closure.", "Corrective", "Monthly", "Trace failed tests to remediation closure.");
        Add("controls", "Segregation of duties review", "SoD conflicts are identified and remediated through periodic review.", "Preventive", "Quarterly", "Run SoD conflict report and verify remediation.");
        Add("controls", "Privileged access recertification", "Privileged access to critical systems is recertified periodically.", "Preventive", "Quarterly", "Verify recertification campaign completion.");
        Add("controls", "Joiner-mover-leaver access control", "Access is provisioned and revoked on JML events within SLA.", "Preventive", "Event-driven", "Sample JML events and verify timely access changes.");
        Add("controls", "Change management approval", "Production changes follow approval, testing and back-out procedures.", "Preventive", "Event-driven", "Inspect a sample of change tickets for approvals.");
        Add("controls", "Reconciliation review", "Key reconciliations are completed, reviewed and breaks cleared.", "Detective", "Daily", "Inspect reconciliation evidence and break ageing.");
        Add("controls", "Service-provider assurance review", "SOC/independent assurance reports for material providers are reviewed and gaps actioned.", "Detective", "Annual", "Verify review of assurance reports and gap actions.");

        // resilience
        Add("resilience", "Tolerance-level approval evidence", "Board approval of tolerance levels for each critical operation is evidenced.", "Directive", "Annual", "Inspect Board approval records for tolerances.");
        Add("resilience", "Resource dependency mapping review", "Resource and dependency maps for critical operations are reviewed and current.", "Detective", "Annual", "Verify currency of dependency maps.");
        Add("resilience", "Single point of failure remediation", "Identified single points of failure for critical operations are tracked and mitigated.", "Corrective", "Quarterly", "Trace SPOFs to mitigation actions.");
        Add("resilience", "Resilience indicator monitoring", "Resilience indicators are monitored and breaches escalated.", "Detective", "Monthly", "Recompute indicators and verify escalation.");

        // critical
        Add("critical", "Critical operations register review", "The critical operations register is reviewed at least annually and on material change.", "Detective", "Annual", "Verify review evidence and change-driven updates.");
        Add("critical", "Critical data backup verification", "Backups of critical data are taken and restorability is verified.", "Detective", "Monthly", "Verify backup logs and test-restore evidence.");
        Add("critical", "Critical operation tolerance monitoring", "Operating performance of critical operations is monitored against tolerance.", "Detective", "Continuous", "Review monitoring dashboards and breach records.");

        // serviceprov
        Add("serviceprov", "Materiality assessment control", "Each new service-provider arrangement has a documented materiality assessment.", "Preventive", "Event-driven", "Sample arrangements for materiality assessments.");
        Add("serviceprov", "Due diligence completion control", "Material arrangements have completed pre-contract due diligence.", "Preventive", "Event-driven", "Sample contracts for due-diligence evidence.");
        Add("serviceprov", "Contract clause compliance control", "Material-arrangement contracts contain required CPS 230 clauses.", "Preventive", "Event-driven", "Inspect contracts for required clauses.");
        Add("serviceprov", "Service-provider SLA monitoring", "Material service-provider SLAs and risk indicators are monitored.", "Detective", "Monthly", "Review SLA monitoring and exception handling.");
        Add("serviceprov", "Material service provider register reconciliation", "The material service provider register is reconciled to contracts and spend.", "Detective", "Quarterly", "Reconcile register to contract and spend data.");

        // incident
        Add("incident", "Incident logging and classification", "Incidents are logged and classified within defined timeframes.", "Detective", "Continuous", "Sample incidents for timely logging and class.");
        Add("incident", "Incident escalation control", "Material incidents are escalated per thresholds.", "Detective", "Event-driven", "Trace sampled incidents to escalation evidence.");
        Add("incident", "Root cause analysis completion", "Material incidents have completed root-cause analysis and actions.", "Corrective", "Event-driven", "Verify RCA records and action closure.");

        // bcp
        Add("bcp", "BCP currency control", "Business continuity plans for critical operations are current and approved.", "Preventive", "Annual", "Verify BCP version, approval and currency.");
        Add("bcp", "Backup and DR readiness", "IT disaster recovery readiness is maintained for critical systems.", "Preventive", "Quarterly", "Inspect DR readiness and configuration evidence.");
        Add("bcp", "Crisis management readiness", "Crisis management contacts, roles and runbooks are current.", "Preventive", "Quarterly", "Verify currency of crisis runbooks and contacts.");

        return list;
    }
}
