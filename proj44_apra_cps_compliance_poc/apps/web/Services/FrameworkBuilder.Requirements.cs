using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

public static partial class FrameworkBuilder
{
    // =====================================================================================
    // REQUIREMENTS  (curated, realistic CPS 230 obligations grouped by theme)
    // REQ-034 and REQ-035 are deliberately left without a mapped policy (known gaps).
    // =====================================================================================
    private static List<RegulatoryRequirement> BuildRequirements()
    {
        var list = new List<RegulatoryRequirement>();
        int n = 1;
        void Add(string theme, string clause, string title, string text, string obligation = "Must") =>
            list.Add(new RegulatoryRequirement
            {
                Id = $"REQ-{n++:000}", Theme = theme, ClauseId = clause,
                Title = title, Text = text, Obligation = obligation
            });

        // governance (CL-01)
        Add("governance", "CL-01", "Board accountability for operational risk",
            "The Board must be accountable for oversight of operational risk management and ensure the entity manages operational risk effectively.");
        Add("governance", "CL-01", "Defined roles and responsibilities",
            "Roles and responsibilities for operational risk, business continuity and service provider management must be clearly defined and documented.");
        Add("governance", "CL-01", "Senior management accountability",
            "Senior management must be accountable for implementing the operational risk management framework approved by the Board.");
        Add("governance", "CL-01", "Risk and control function independence",
            "The entity must maintain risk and control functions with sufficient authority, independence and resources.");

        // framework (CL-02)
        Add("framework", "CL-02", "Maintain an operational risk management framework",
            "The entity must maintain an operational risk management framework appropriate to its size, business mix and complexity.");
        Add("framework", "CL-02", "Identify and assess operational risks",
            "The entity must identify and assess its operational risks, including from people, processes, systems, data and external events.");
        Add("framework", "CL-02", "Monitor the operational risk profile",
            "The entity must monitor its operational risk profile against its risk appetite and report to the Board and senior management.");
        Add("framework", "CL-02", "Timely remediation of control weaknesses",
            "The entity must remediate operational risk control weaknesses and deficiencies in a timely manner.");
        Add("framework", "CL-02", "Risk assessment of new and changed processes",
            "The entity must assess the operational risk impact of material changes to its business operations, including new products and systems.");

        // controls (CL-03)
        Add("controls", "CL-03", "Design and implement internal controls",
            "The entity must design, implement and embed internal controls to prevent and detect operational risk events.");
        Add("controls", "CL-03", "Assess control design and operating effectiveness",
            "The entity must regularly assess the design and operating effectiveness of its operational risk controls.");
        Add("controls", "CL-03", "Controls over service-provider activities",
            "The entity must ensure effective controls are in place over activities and processes performed by service providers.");
        Add("controls", "CL-03", "Control monitoring and assurance",
            "The entity must monitor controls and obtain assurance over their effectiveness, escalating failures.");

        // resilience (CL-04)
        Add("resilience", "CL-04", "Maintain critical operations within tolerance",
            "The entity must maintain the ability to continue critical operations within tolerance levels through severe disruptions.");
        Add("resilience", "CL-04", "Board-approved tolerance levels",
            "The Board must approve tolerance levels for each critical operation, expressed as the maximum acceptable level of disruption.");
        Add("resilience", "CL-04", "Resource and capability assessment for resilience",
            "The entity must assess whether it has the people, processes, technology and facilities to operate within tolerance.");
        Add("resilience", "CL-04", "Manage resilience risk from interdependencies",
            "The entity must understand and manage interdependencies and concentrations that may affect its operational resilience.");

        // critical (CL-05)
        Add("critical", "CL-05", "Identify critical operations",
            "The entity must identify its critical operations and maintain a register, reviewed at least annually.");
        Add("critical", "CL-05", "Map resources supporting critical operations",
            "The entity must identify and document the people, processes, technology, facilities and information supporting each critical operation.");
        Add("critical", "CL-05", "Assess impact of disruption to critical operations",
            "The entity must assess the potential impact of disruption to each critical operation on depositors, policyholders and the financial system.");

        // serviceprov (CL-06)
        Add("serviceprov", "CL-06", "Maintain a service provider management policy",
            "The entity must maintain a service provider management policy approved by the Board covering the end-to-end lifecycle.");
        Add("serviceprov", "CL-06", "Maintain a register of material service providers",
            "The entity must maintain a register of its material service providers and material arrangements.");
        Add("serviceprov", "CL-06", "Due diligence before engaging service providers",
            "The entity must conduct due diligence before entering into a material arrangement with a service provider.");
        Add("serviceprov", "CL-06", "Formal agreements for material arrangements",
            "The entity must have a formal, legally binding agreement for each material arrangement covering required matters.");
        Add("serviceprov", "CL-06", "Ongoing monitoring of material service providers",
            "The entity must monitor the performance and risks of material service providers on an ongoing basis.");
        Add("serviceprov", "CL-06", "Manage concentration and fourth-party risk",
            "The entity must assess concentration risk and the risks arising from a service provider's own material sub-contractors.");

        // incident (CL-07)
        Add("incident", "CL-07", "Identify and escalate operational risk incidents",
            "The entity must be able to identify, record, escalate and respond to operational risk incidents.");
        Add("incident", "CL-07", "Manage disruptions to restore within tolerance",
            "The entity must manage disruptions to critical operations to restore them within tolerance levels.");
        Add("incident", "CL-07", "Learn from incidents",
            "The entity must analyse incidents to identify root causes and prevent recurrence.");

        // bcp (CL-08)
        Add("bcp", "CL-08", "Maintain a business continuity plan",
            "The entity must maintain a business continuity plan setting out how critical operations are maintained within tolerance through disruptions.");
        Add("bcp", "CL-08", "BCP resources and recovery arrangements",
            "The BCP must document the resources, actions, recovery strategies and arrangements required to respond to disruptions.");

        // testing (CL-09)
        Add("testing", "CL-09", "Scenario testing of resilience",
            "The entity must conduct scenario testing of its ability to maintain critical operations within tolerance under severe but plausible scenarios.");
        Add("testing", "CL-09", "Annual review of the BCP",
            "The entity must review and test its business continuity plan at least annually.");
        Add("testing", "CL-09", "Act on testing results",
            "The entity must address deficiencies identified through scenario testing and BCP exercises.");

        // notification (CL-10)  -- REQ-034 and REQ-035 are deliberately left WITHOUT a policy (known gaps)
        Add("notification", "CL-10", "Notify APRA of material operational risk incidents",
            "The entity must notify APRA as soon as possible and within 72 hours of an operational risk incident likely to have a material financial impact or material impact on critical operations.");
        Add("notification", "CL-10", "Notify APRA of material service provider arrangements",
            "The entity must notify APRA prior to entering into, or on becoming aware of a material change to, a material service provider arrangement.");
        Add("notification", "CL-10", "Notify APRA of BCP activation and tolerance breaches",
            "The entity must notify APRA when it has suffered a disruption that has resulted in a critical operation operating outside tolerance.");

        return list;
    }
}
