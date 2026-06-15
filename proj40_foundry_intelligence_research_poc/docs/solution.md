# Solution — proj40 Foundry Intelligence & Research POC

## Problem
A large enterprise wants to triage inbound customer documents (RFPs, briefing notes, incident reports)
that arrive by email, automatically understand them, enrich them against internal and external
knowledge, and produce a research brief plus a stakeholder-ready summary email — using Microsoft
Foundry agent capabilities, deployed as a .NET Azure web app.

## Approach
A 5-stage agent pipeline, each stage traceable, exposed as distinct UI tabs:

1. **Entity extraction** — read the email + attached document and extract the key entities
   (organisations, people, topics, technologies, locations, amounts, dates, industry, intent). These
   are the anchors for everything downstream.
2. **Insight generation** — turn the email + document into categorised insights (Need / Risk /
   Opportunity / Context / Signal), each with an *evidence* note for traceability.
3. **Source intelligence** — pull records from a mocked corpus of **internal** (CRM, delivery
   knowledge base, compliance playbooks) and **external** (news wire, market intelligence, analyst
   guides, regulator briefs) sources, matched to the extracted entities. Results are cited `[S1]…[Sn]`.
4. **Research Agent** — synthesise a brief: executive summary, key findings (with citations), risks,
   opportunities, recommended actions, open questions, and a confidence rating.
5. **Report email** — compose a send-ready internal email routed to the relevant industry vertical
   (energy / retail / FSI / health / general), summarising the insights in skimmable sections, with a
   sources appendix; downloadable as markdown.

## Two engines, automatic selection
- **`OfflineResearchEngine`** — deterministic, no external calls. Default, so the POC always runs.
- **`FoundryResearchEngine`** — live Microsoft Foundry prompt agent via
  `AIProjectClient.AsAIAgent(...)` (Microsoft Agent Framework). Stages 1, 2, 4, 5 are grounded
  JSON prompt-agent calls; stage 3 stays deterministic because the corpus is mocked and owned by us
  (keeps source pulls auditable). On **any** failure it falls back to the offline engine and records
  the reason.

Selection is automatic: live only when `Foundry:Enabled=true` **and** `ProjectEndpoint` is set.

## Why this shape
- **Entity-keyed enrichment** mirrors how a real research desk works and makes the "pull mocked
  sources" requirement concrete and traceable — the same `SourceCorpus.Pull(entities)` seam accepts
  real connectors (CRM API, news API, Azure AI Search) without changing the pipeline.
- **Evidence + citations on every output** keeps the AI auditable, which is the whole point of an
  enterprise research assistant.
- **Offline-first** guarantees a reliable demo regardless of Azure quota/auth.

## Azure footprint (Bicep)
App Service (Linux, `DOTNETCORE|10.0`, S1) · Microsoft Foundry AI Services + project + `gpt-4o`
(GlobalStandard) · Storage (StorageV2, **keyless**, shared-key disabled, blob RBAC) · Key Vault
(RBAC) · Log Analytics + Application Insights. The web app's **system-assigned managed identity** gets
least-privilege RBAC: Storage Blob Data Contributor, Key Vault Secrets User, Cognitive Services User,
Cognitive Services OpenAI User. `httpsOnly`, TLS 1.2, FTPS disabled, `disableLocalAuth` on Foundry.

## Security / guardrails
- Keyless auth end-to-end (`DefaultAzureCredential` + managed identity).
- Spam/non-genuine inbound is detected and **quarantined** (no research, routed to triage).
- Model is instructed to answer strictly from supplied text/sources and never invent facts.
- All data here is mock/demo and labelled as not for production decisions.
