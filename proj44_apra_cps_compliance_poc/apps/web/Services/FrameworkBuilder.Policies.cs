using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

public static partial class FrameworkBuilder
{
    // =====================================================================================
    // POLICIES  (>=130)  -- realistic CPS 230 policy items grouped by theme/domain.
    // POL-128, POL-129, POL-130 are deliberately left without a mapped standard (known gaps).
    // =====================================================================================
    private static List<Policy> BuildPolicies()
    {
        var list = new List<Policy>();
        int n = 1;
        void Add(string theme, string title, string statement, string owner)
        {
            var domain = Cps230Seed.Themes.First(t => t.Key == theme).PolicyDomain;
            list.Add(new Policy { Id = $"POL-{n++:000}", Domain = domain, Title = title, Statement = statement, Owner = owner });
        }

        // ---- governance : Governance & Accountability (12) ----
        Add("governance", "Board oversight of operational risk", "The Board oversees operational risk management and approves the operational risk appetite and tolerance levels.", "Board Risk Committee");
        Add("governance", "Operational risk governance structure", "A documented governance structure assigns operational risk accountabilities across three lines of defence.", "Chief Risk Officer");
        Add("governance", "Roles and responsibilities", "Roles and responsibilities for operational risk, resilience and service provider management are defined and reviewed annually.", "Chief Risk Officer");
        Add("governance", "Operational risk appetite statement", "The entity maintains a Board-approved operational risk appetite statement aligned to its strategy.", "Chief Risk Officer");
        Add("governance", "Senior management accountability", "Accountable executives are assigned for each operational risk domain consistent with the accountability regime.", "Chief Executive Officer");
        Add("governance", "Risk culture", "The entity promotes a sound operational risk culture with clear tone from the top.", "Chief Risk Officer");
        Add("governance", "Three lines of defence", "Operational risk is managed under a three-lines-of-defence model with independent assurance.", "Chief Risk Officer");
        Add("governance", "Board reporting of operational risk", "The Board receives regular reporting on the operational risk profile, incidents and resilience.", "Chief Risk Officer");
        Add("governance", "Operational risk policy governance", "Operational risk policies are owned, version-controlled and reviewed at least annually or on material change.", "Head of Risk Policy");
        Add("governance", "Conflicts of interest in risk functions", "Risk and control functions are independent and free from conflicts that could impair objectivity.", "Chief Risk Officer");
        Add("governance", "Remuneration and operational risk", "Remuneration arrangements consider operational risk outcomes and accountability.", "People & Culture");
        Add("governance", "Operational risk training and awareness", "Staff receive role-appropriate operational risk and resilience training.", "People & Culture");

        // ---- framework : Operational Risk Management (14) ----
        Add("framework", "Operational risk management framework", "The entity maintains an operational risk management framework proportionate to its size and complexity.", "Chief Risk Officer");
        Add("framework", "Risk identification", "Operational risks are systematically identified across people, process, systems, data and external events.", "Head of Operational Risk");
        Add("framework", "Risk and control self-assessment", "Business units perform periodic risk and control self-assessments (RCSA).", "Head of Operational Risk");
        Add("framework", "Operational risk taxonomy", "A standard operational risk taxonomy is used to classify risks and events consistently.", "Head of Operational Risk");
        Add("framework", "Key risk indicators", "Key risk indicators are defined, thresholds set, and breaches escalated.", "Head of Operational Risk");
        Add("framework", "Operational risk appetite monitoring", "The operational risk profile is monitored against appetite and reported to governance forums.", "Head of Operational Risk");
        Add("framework", "Issue and action management", "Operational risk issues and remediation actions are tracked to closure with due dates and owners.", "Head of Operational Risk");
        Add("framework", "Material change risk assessment", "Material changes, new products and system changes undergo operational risk assessment before go-live.", "Head of Operational Risk");
        Add("framework", "Data risk management", "Risks to the confidentiality, integrity and availability of critical data are identified and managed.", "Chief Data Officer");
        Add("framework", "Technology and cyber operational risk", "Technology and cyber risks are managed as part of the operational risk framework.", "Chief Information Security Officer");
        Add("framework", "People and conduct risk", "People-related operational risks, including key-person and conduct risk, are managed.", "People & Culture");
        Add("framework", "Process risk management", "Critical business processes are documented and their operational risks assessed.", "Head of Operational Risk");
        Add("framework", "External event risk", "Risks from external events (natural hazards, pandemic, geopolitical) are assessed and planned for.", "Head of Operational Risk");
        Add("framework", "Operational loss event data", "Operational loss events are captured and used to inform risk assessment and, where relevant, capital.", "Head of Operational Risk");

        // ---- controls : Risk Controls & Assurance (12) ----
        Add("controls", "Internal control framework", "The entity designs and embeds internal controls to manage operational risks across critical processes.", "Head of Operational Risk");
        Add("controls", "Control design standards", "Controls are designed to address identified risks with clear ownership and operating frequency.", "Head of Controls");
        Add("controls", "Control effectiveness testing", "The design and operating effectiveness of key controls is tested on a risk-based cycle.", "Head of Controls");
        Add("controls", "Control monitoring", "Control performance is monitored and failures are escalated and remediated.", "Head of Controls");
        Add("controls", "Service provider control assurance", "Assurance is obtained over controls operated by material service providers.", "Head of Controls");
        Add("controls", "Segregation of duties", "Segregation of duties is enforced for sensitive operational processes.", "Head of Controls");
        Add("controls", "Change control", "Changes to critical systems and processes follow a controlled change-management process.", "Head of Technology");
        Add("controls", "Access control over critical systems", "Access to systems supporting critical operations is restricted and reviewed.", "Chief Information Security Officer");
        Add("controls", "Reconciliation controls", "Key reconciliations are performed and reviewed to detect processing errors.", "Head of Finance Operations");
        Add("controls", "Control assurance reporting", "Results of control testing and assurance are reported to risk governance forums.", "Head of Controls");
        Add("controls", "Independent assurance and audit", "Internal audit provides independent assurance over the operational risk and control environment.", "Chief Audit Executive");
        Add("controls", "Control issue remediation", "Control deficiencies are remediated within agreed timeframes and verified.", "Head of Controls");

        // ---- resilience : Operational Resilience (10) ----
        Add("resilience", "Operational resilience policy", "The entity maintains an operational resilience policy to continue critical operations through disruption.", "Head of Resilience");
        Add("resilience", "Tolerance levels for critical operations", "Board-approved tolerance levels define the maximum acceptable disruption for each critical operation.", "Head of Resilience");
        Add("resilience", "Resilience resourcing", "The people, processes, technology and facilities required to operate within tolerance are identified and maintained.", "Head of Resilience");
        Add("resilience", "Interdependency and concentration management", "Interdependencies and concentrations affecting resilience are mapped and managed.", "Head of Resilience");
        Add("resilience", "Resilience risk assessment", "Resilience risks to critical operations are assessed and treated.", "Head of Resilience");
        Add("resilience", "Recovery objectives", "Recovery time and recovery point objectives for critical operations are defined consistent with tolerance levels.", "Head of Resilience");
        Add("resilience", "Resilience by design", "Resilience requirements are embedded into the design of new critical processes and systems.", "Head of Resilience");
        Add("resilience", "Resilience governance and reporting", "Resilience status and tolerance breaches are reported to the Board and senior management.", "Head of Resilience");
        Add("resilience", "Facility and geographic resilience", "Critical operations have resilient facilities and alternate sites where required.", "Head of Property & Facilities");
        Add("resilience", "Workforce resilience", "Workforce continuity arrangements, including key-person and skills redundancy, are maintained.", "People & Culture");

        // ---- critical : Critical Operations (8) ----
        Add("critical", "Critical operations identification", "Critical operations are identified using defined criteria and maintained in a register.", "Head of Resilience");
        Add("critical", "Critical operations register", "A register records each critical operation, its owner, tolerance and supporting resources.", "Head of Resilience");
        Add("critical", "Critical operations resource mapping", "Each critical operation is mapped to its supporting people, processes, technology, facilities and data.", "Head of Resilience");
        Add("critical", "Critical operations impact assessment", "The impact of disruption to each critical operation is assessed and documented.", "Head of Resilience");
        Add("critical", "Critical operations review", "The critical operations register is reviewed at least annually and on material change.", "Head of Resilience");
        Add("critical", "Critical third-party dependency mapping", "Dependencies of critical operations on service providers are identified and managed.", "Head of Resilience");
        Add("critical", "Critical data identification", "Data critical to critical operations is identified and protected.", "Chief Data Officer");
        Add("critical", "Critical operations interdependency mapping", "Internal interdependencies between critical operations are mapped.", "Head of Resilience");

        // ---- serviceprov : Third-Party & Service Provider Risk (14) ----
        Add("serviceprov", "Service provider management policy", "A Board-approved policy governs the end-to-end lifecycle of service provider arrangements.", "Head of Third-Party Risk");
        Add("serviceprov", "Material service provider register", "A register of material service providers and material arrangements is maintained and kept current.", "Head of Third-Party Risk");
        Add("serviceprov", "Service provider due diligence", "Due diligence is performed before entering into a material arrangement.", "Head of Third-Party Risk");
        Add("serviceprov", "Service provider risk assessment", "Risks of each material arrangement are assessed prior to and during the engagement.", "Head of Third-Party Risk");
        Add("serviceprov", "Formal service provider agreements", "Material arrangements are governed by formal, legally binding agreements covering required matters.", "Head of Procurement");
        Add("serviceprov", "Service provider performance monitoring", "Performance and service levels of material service providers are monitored on an ongoing basis.", "Head of Third-Party Risk");
        Add("serviceprov", "Service provider concentration risk", "Concentration risk across service providers is assessed and managed.", "Head of Third-Party Risk");
        Add("serviceprov", "Fourth-party and subcontractor risk", "Risks from service providers' material subcontractors (fourth parties) are identified and managed.", "Head of Third-Party Risk");
        Add("serviceprov", "Service provider exit and substitutability", "Exit strategies and substitutability are assessed for material arrangements.", "Head of Third-Party Risk");
        Add("serviceprov", "Service provider information security", "Material service providers meet the entity's information security and data protection requirements.", "Chief Information Security Officer");
        Add("serviceprov", "Offshoring risk management", "Risks specific to offshoring of material arrangements are assessed and managed.", "Head of Third-Party Risk");
        Add("serviceprov", "Service provider business continuity", "Material service providers maintain business continuity arrangements aligned to the entity's tolerance.", "Head of Third-Party Risk");
        Add("serviceprov", "Intragroup arrangements", "Intragroup material arrangements are managed with the same rigour as external arrangements.", "Head of Third-Party Risk");
        Add("serviceprov", "Service provider audit and access rights", "Agreements preserve the entity's and APRA's rights to access, audit and obtain information.", "Head of Procurement");

        // ---- incident : Incident Management (9) ----
        Add("incident", "Operational risk incident management", "Operational risk incidents are identified, recorded, escalated and responded to under a defined process.", "Head of Operational Risk");
        Add("incident", "Incident classification and severity", "Incidents are classified by severity to drive escalation and response.", "Head of Operational Risk");
        Add("incident", "Incident escalation", "Material incidents are escalated to senior management, the Board and regulators as required.", "Head of Operational Risk");
        Add("incident", "Incident response and recovery", "Defined response and recovery procedures restore critical operations within tolerance.", "Head of Resilience");
        Add("incident", "Root cause analysis", "Root cause analysis is performed for material incidents to prevent recurrence.", "Head of Operational Risk");
        Add("incident", "Incident register", "A central register records operational risk incidents and their resolution.", "Head of Operational Risk");
        Add("incident", "Crisis management", "A crisis management capability coordinates response to severe disruptions.", "Head of Resilience");
        Add("incident", "Post-incident review", "Post-incident reviews capture lessons learned and feed remediation.", "Head of Operational Risk");
        Add("incident", "Cyber incident response", "Cyber incidents affecting critical operations are managed under an aligned response plan.", "Chief Information Security Officer");

        // ---- bcp : Business Continuity (9) ----
        Add("bcp", "Business continuity policy", "A business continuity policy governs how critical operations are maintained through disruption.", "Head of Resilience");
        Add("bcp", "Business continuity plans", "Business continuity plans document strategies, resources and actions to maintain critical operations within tolerance.", "Head of Resilience");
        Add("bcp", "Recovery strategies", "Recovery strategies are defined for the people, technology and facilities supporting critical operations.", "Head of Resilience");
        Add("bcp", "Alternate processing arrangements", "Alternate processing and workaround arrangements support continuity of critical operations.", "Head of Resilience");
        Add("bcp", "BCP roles and responsibilities", "Roles and responsibilities for business continuity are defined and communicated.", "Head of Resilience");
        Add("bcp", "Emergency and crisis communications", "Communication plans support stakeholders during disruptions.", "Head of Corporate Affairs");
        Add("bcp", "IT disaster recovery", "IT disaster recovery plans align to business continuity recovery objectives.", "Head of Technology");
        Add("bcp", "Pandemic and workforce continuity", "Plans address workforce unavailability, including pandemic scenarios.", "People & Culture");
        Add("bcp", "Third-party continuity dependencies", "Continuity arrangements account for dependencies on material service providers.", "Head of Resilience");

        // ---- testing : Resilience Testing (8) ----
        Add("testing", "Scenario testing programme", "A programme of scenario testing assesses the ability to maintain critical operations within tolerance.", "Head of Resilience");
        Add("testing", "Severe-but-plausible scenarios", "Severe but plausible scenarios are designed, including cyber, third-party and facility loss.", "Head of Resilience");
        Add("testing", "BCP testing and exercising", "Business continuity plans are tested and exercised at least annually.", "Head of Resilience");
        Add("testing", "Disaster recovery testing", "IT disaster recovery capabilities are tested against recovery objectives.", "Head of Technology");
        Add("testing", "Testing of service provider arrangements", "Resilience of material service provider arrangements is tested or evidenced.", "Head of Third-Party Risk");
        Add("testing", "Test result remediation", "Deficiencies identified in testing are remediated and re-tested.", "Head of Resilience");
        Add("testing", "Independent review of testing", "The scenario testing programme is subject to independent review.", "Chief Audit Executive");
        Add("testing", "Testing governance and reporting", "Testing scope, results and remediation are reported to the Board.", "Head of Resilience");

        // ---- Additional domain depth so the library reflects a real CPS 230 policy suite (>=130 total). ----
        // governance depth
        Add("governance", "Operational risk committee charter", "A management operational risk committee operates under a charter with defined membership and mandate.", "Chief Risk Officer");
        Add("governance", "Risk function resourcing", "The operational risk function is adequately resourced with appropriate skills and tooling.", "Chief Risk Officer");
        Add("governance", "Policy framework hierarchy", "A documented hierarchy links Board policies, standards and procedures for operational risk.", "Head of Risk Policy");
        Add("governance", "Operational risk delegations", "Decision-making delegations for operational risk acceptance are defined and enforced.", "Chief Risk Officer");

        // framework depth
        Add("framework", "Emerging risk identification", "Emerging operational risks are horizon-scanned and assessed.", "Head of Operational Risk");
        Add("framework", "Model risk in operational processes", "Operational risks arising from models and automation are assessed and managed.", "Head of Model Risk");
        Add("framework", "Fraud risk management", "Internal and external fraud risks are assessed and mitigated.", "Head of Financial Crime");
        Add("framework", "Legal and compliance operational risk", "Legal and regulatory-compliance operational risks are identified and managed.", "General Counsel");
        Add("framework", "Records and information management risk", "Risks to records and information lifecycle management are managed.", "Chief Data Officer");

        // controls depth
        Add("controls", "Preventive control standards", "Preventive controls are prioritised for high-impact operational risks.", "Head of Controls");
        Add("controls", "Detective control standards", "Detective controls provide timely identification of control failures and incidents.", "Head of Controls");
        Add("controls", "Manual control oversight", "Manual controls are documented, evidenced and subject to quality review.", "Head of Controls");
        Add("controls", "Automated control assurance", "Automated controls are validated and monitored for continued effectiveness.", "Head of Technology");
        Add("controls", "Key control inventory", "A complete inventory of key controls is maintained and mapped to risks.", "Head of Controls");

        // resilience / critical / serviceprov depth
        Add("resilience", "Resilience metrics and indicators", "Resilience indicators track the entity's ability to operate within tolerance.", "Head of Resilience");
        Add("resilience", "Substitutability of critical resources", "Substitutability and redundancy of resources supporting critical operations are assessed.", "Head of Resilience");
        Add("critical", "Critical operations tolerance setting", "A defined methodology sets and reviews tolerance levels for each critical operation.", "Head of Resilience");
        Add("critical", "Critical operations change assessment", "Changes affecting critical operations are assessed for resilience impact.", "Head of Resilience");
        Add("serviceprov", "Service provider lifecycle governance", "A governance forum oversees material service provider arrangements across their lifecycle.", "Head of Third-Party Risk");
        Add("serviceprov", "Cloud service provider risk", "Risks specific to cloud and technology service providers are assessed and managed.", "Chief Information Security Officer");
        Add("serviceprov", "Service provider data sovereignty", "Data location and sovereignty requirements for material arrangements are defined.", "Chief Data Officer");

        // incident / bcp / testing depth
        Add("incident", "Incident notification thresholds", "Internal thresholds determine when incidents require senior and regulatory notification.", "Head of Operational Risk");
        Add("incident", "Incident data quality", "Incident data is complete, accurate and timely to support analysis and reporting.", "Head of Operational Risk");
        Add("bcp", "BCP maintenance and version control", "Business continuity plans are maintained, version-controlled and kept current.", "Head of Resilience");
        Add("bcp", "Manual workaround procedures", "Documented manual workarounds support critical operations during system outages.", "Head of Resilience");
        Add("testing", "Integrated resilience exercises", "Integrated exercises test people, process, technology and third-party response together.", "Head of Resilience");
        Add("testing", "Lessons-learned integration", "Lessons from tests and live incidents are integrated into plans and controls.", "Head of Resilience");

        // ---- notification : Regulatory Engagement (Board-level intent). ----
        // The final three (POL-128, POL-129, POL-130) are deliberately left WITHOUT a mapped standard
        // to create real Gap Analysis findings (policy -> standard orphans).
        Add("notification", "APRA notification policy", "The entity maintains a policy governing notifications and reporting to APRA for operational risk matters.", "Head of Regulatory Affairs");
        Add("notification", "Material incident notification", "Operational risk incidents likely to have a material impact are notified to APRA within required timeframes.", "Head of Regulatory Affairs");
        Add("notification", "Material arrangement notification", "APRA is notified of new or materially changed material service provider arrangements.", "Head of Regulatory Affairs");
        Add("notification", "Regulatory relationship management", "The entity maintains an open, constructive and cooperative relationship with APRA.", "Head of Regulatory Affairs");
        Add("notification", "Regulatory reporting accuracy", "Information provided to APRA is accurate, complete and provided on a timely basis.", "Head of Regulatory Affairs");
        Add("notification", "Breach and tolerance reporting", "Tolerance breaches affecting critical operations are reported to APRA without delay.", "Head of Regulatory Affairs");
        Add("notification", "Regulatory change management", "Changes to prudential requirements are tracked and implemented through governance.", "Head of Regulatory Affairs");

        return list;
    }
}
