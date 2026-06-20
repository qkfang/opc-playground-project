namespace Proj37.CostEstimator.Web.Services;

/// <summary>
/// Single source of truth for the per-step agent instructions shown to users (in the UI popups)
/// and used to ground each estimation step. Each step is run as an individual agent-backed step:
/// Scope, Requirements, and Cost Model. Exposing the instructions makes the pipeline transparent
/// and auditable.
/// </summary>
public static class AgentInstructions
{
    public const string SystemPersona =
        "You are a senior Azure solutions architect and FinOps specialist. You read technical " +
        "documents, infer scope, derive technical requirements, and design a cost-efficient Azure " +
        "architecture. Always respond with ONLY a single valid JSON object matching the requested " +
        "schema — no markdown, no prose, no code fences. Prefer managed PaaS services. Be realistic " +
        "and conservative with sizing.";

    public sealed record StepInstruction(string Key, string Title, string Agent, string Goal, string Instructions);

    public static readonly StepInstruction Scope = new(
        Key: "scope",
        Title: "Scope",
        Agent: "Scope Analyst agent",
        Goal: "Read the uploaded technical documents and produce a structured scope summary.",
        Instructions:
            "Analyze the supplied technical document(s) and produce a SCOPE summary.\n\n" +
            "Return a single JSON object with exactly these fields:\n" +
            "  projectName       — the project / system name\n" +
            "  overview          — 2-3 sentence summary of what is being built\n" +
            "  businessGoal      — the business outcome the solution drives\n" +
            "  inScope[]         — capabilities explicitly in scope\n" +
            "  outOfScope[]      — explicitly excluded items\n" +
            "  assumptions[]     — sizing / pricing / deployment assumptions\n" +
            "  workloadProfile   — e.g. \"web front end + API + AI workload + relational data store\"\n" +
            "  expectedScale     — e.g. \"~5k MAU, ~50 req/s peak\"\n" +
            "  dataSensitivity   — e.g. \"internal business data\" | \"contains PII\" | \"regulated\"\n" +
            "  environment       — \"production\" | \"non-production (POC/dev)\"\n\n" +
            "Infer scale and data sensitivity from the documents. Do not invent requirements that are not " +
            "supported by the source text.");

    public static readonly StepInstruction Requirements = new(
        Key: "requirements",
        Title: "Requirements",
        Agent: "Requirements Engineer agent",
        Goal: "Turn the agreed scope and source documents into concrete technical requirements.",
        Instructions:
            "Given the SCOPE produced by the previous step and the source documents, derive the technical " +
            "requirements for an Azure solution.\n\n" +
            "Return JSON: { \"requirements\": [ { id, category, requirement, rationale, priority } ] }\n" +
            "  id        — REQ-001, REQ-002, …\n" +
            "  category  — Compute | Data | Networking | Security | AI | Observability\n" +
            "  priority  — Must | Should | Could\n\n" +
            "Cover compute, data, AI/Foundry (if relevant), security (managed identity, Key Vault), " +
            "networking (HTTPS-only), and observability. Produce 8-14 requirements, each traceable to the " +
            "scope or a document statement.");

    public static readonly StepInstruction Cost = new(
        Key: "cost",
        Title: "Cost Model",
        Agent: "Cost Architect agent",
        Goal: "Design the concrete Azure service plan and quantities; the app prices it deterministically.",
        Instructions:
            "Design the concrete Azure service plan to run this workload for ONE month, then estimate " +
            "quantities. You decide services / SKUs / quantities; do NOT compute dollar totals — the " +
            "application prices each line item with reference rates so the math stays deterministic and " +
            "auditable.\n\n" +
            "Return JSON: { \"services\": [ { service, sku, category, meter, assumption, quantity, " +
            "unitPrice, unit } ], \"contingencyPercent\": number }\n\n" +
            "Always include: compute (App Service), storage (Blob), observability (Log Analytics), security " +
            "(Key Vault). Include Foundry / Azure OpenAI token line items if the workload uses AI, and Azure " +
            "AI Search if it uses document / file search. contingencyPercent is a 15-30 risk buffer. " +
            "Quantities feed an editable Cost Model where reviewers can adjust each row.");

    public static readonly IReadOnlyList<StepInstruction> All = new[] { Scope, Requirements, Cost };
}
