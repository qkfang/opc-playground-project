# Solution Design — proj43 FinOps Foundry Chatbot (with Fabric MCP)

## Problem
Enterprise FinOps teams need fast, conversational answers about cloud cost, usage, commitment
coverage, anomalies, and optimisation — grounded in their own governed data (which in this
organisation lives in **Microsoft Fabric / OneLake**). They want a chat experience, not another
dashboard, and it must be a real enterprise app (auth-ready, observable, deployable), not a mock.

## What we are building
A **.NET 10 web application** that hosts a **conversational FinOps assistant**. The assistant is a
**Microsoft Foundry agent** (Microsoft Agent Framework, hosted in-process via
`AIProjectClient.AsAIAgent(...)`) that can call **Microsoft Fabric data** as a tool to answer
cost/usage questions over governed enterprise data.

Two tool/data paths are supported behind one `IFinOpsAgent` abstraction:

1. **Live Foundry path** (`FoundryFinOpsAgent`) — a streaming, multi-turn agent. Fabric data access is
   wired two ways, selectable by config:
   - **Fabric data agent tool** (`Fabric:ConnectionId`) — the documented Foundry → Fabric data agent
     integration (identity passthrough / On-Behalf-Of). The Foundry agent treats the published Fabric
     data agent as a knowledge tool and does NL2SQL over OneLake (warehouse/lakehouse/semantic model).
   - **MCP tool** (`Mcp:Endpoint` / local stdio) — a Model Context Protocol server exposing Fabric
     query tools, attached via `McpClientFactory` → `ListToolsAsync()` → `AITool[]`. This is the
     generic, future-proof "MCP access to Fabric" path requested in the brief.
2. **Offline FinOps engine** (`OfflineFinOpsAgent`, default) — a deterministic, grounded assistant
   backed by a seeded **FinOps sample dataset** (12 months of cost/usage across subscriptions,
   services, resource groups, tags). It answers the same FinOps questions (spend, trend, top services,
   anomalies, commitment coverage, savings recommendations) with real arithmetic. This guarantees the
   POC is always demonstrable and testable with **no live Azure/Fabric dependency**, and it is the
   exact behaviour the live agent reproduces against real Fabric data.

The engine is selected at startup: live when `Foundry:Enabled` + endpoint configured, else offline.
The live engine also **falls back to offline** on any runtime failure (auth/quota/transient), so the
chat never dead-ends.

## Architecture

| Layer | Choice | Why |
| --- | --- | --- |
| Web app | ASP.NET Core (.NET 10), Razor Pages + minimal API | Matches App Service `DOTNETCORE\|10.0`; one deployable; minimal API exposes a clean chat + OpenAPI surface. |
| Chat transport | **Server-Sent Events (SSE)** streaming from `/api/chat/stream` | Real-time token streaming for a responsive chat without a SPA build chain or websockets. |
| AI reasoning | Microsoft Foundry agent via **Microsoft Agent Framework** (`AIProjectClient.AsAIAgent`) + `RunStreamingAsync` + `AgentSession` | Documented hosted/in-process pattern; native multi-turn conversation via `AgentSession`; full code control. |
| Fabric data | **Fabric data agent tool** (connection ID) and/or **MCP tool** (`McpClientFactory`) | Both are first-party documented ways to give a Foundry agent governed Fabric data; brief explicitly asks for MCP access to Fabric. |
| Offline data | `FinOpsDataset` (seeded JSON) + `OfflineFinOpsAgent` + `FinOpsAnalytics` | Deterministic, auditable FinOps answers; always available for demo/CI. |
| Conversation state | In-memory `ConversationStore` keyed by session id (sliding window) | Zero-dependency POC multi-turn memory; production-upgradeable to Redis/Cosmos. |
| Persistence | File-based JSON transcripts under `App_Data` (optional) | Zero-dependency POC; production-upgradeable to Blob (`Storage:AccountUrl`). |
| Telemetry | Application Insights (when connection string present) | Standard ASP.NET Core integration. |
| Infra | Bicep (App Service, Foundry, Storage, Key Vault, monitoring) + RBAC, managed identity | Mirrors proj37/template; keyless. |
| CI/CD | GitHub Actions (`proj43_*_infra` + `proj43_*_deploy`) | Repo conventions (`rg-playground-01`, `AZURE_CREDENTIALS`, `azure/login@v2`). |

## Chat UI decision — why NOT AG UI (evaluated)
The brief asked to evaluate existing chat patterns including **AG UI** and use it only if it genuinely
fits. Evaluation:

- **AG UI / CopilotKit** is a TypeScript/React, event-protocol frontend (`@ag-ui/*`, CopilotKit
  runtime). It shines when the host app is already a React SPA and you want a drop-in agentic chat with
  generative UI, and when the backend speaks the AG UI event protocol.
- **This app is a server-rendered .NET enterprise POC.** Bolting on a React/Node build chain (Vite,
  npm, a separate dev server, an AG UI ⇄ Foundry protocol bridge) would add a second runtime, a second
  deploy artifact, and protocol-translation glue — for a single chat surface. That hurts the
  "one deployable .NET app", testability, and the App Service hosting story.
- **Decision: server-rendered .NET chat UI** — Razor page + a small (~no-build, vanilla) JS chat
  client that streams tokens over SSE, renders Markdown (Markdig server-side for tool/data tables),
  shows tool-call activity ("Querying Fabric…"), and keeps conversation in a session. This matches the
  reused proj37 stack, ships as one artifact, is trivially testable, and still looks enterprise-grade.
- We keep the door open: the same `/api/chat/stream` SSE contract is exactly what an AG UI/CopilotKit
  frontend (or Teams, or a React shell) could consume later. So choosing the .NET UI now does not
  burn the AG UI option — it is documented as a future adapter.

Reuse: the .NET app skeleton, Foundry options/engine-selection pattern, offline-fallback design,
layout/CSS, tests, bicep modules and workflows are **adapted from proj37** (Foundry Cost Estimator)
which already proved the Foundry Agent Framework + offline-fallback approach in this repo.

## FinOps capabilities (what you can ask)
The assistant understands and answers (offline engine computes; live agent does the same over Fabric):
- **Total / period spend** — "What did we spend last month?", "Spend MTD vs last month".
- **Trend** — "How is our spend trending over the last 6 months?".
- **Top cost drivers** — "Top 5 services / resource groups / subscriptions by cost".
- **Anomalies** — "Any cost spikes recently?" (month-over-month % jump detection).
- **Commitment coverage** — reservation/savings-plan coverage % and on-demand exposure.
- **Optimisation** — idle/underused resources, rightsizing & commitment recommendations with
  estimated monthly savings.
- **Tag / showback** — cost by `costCenter` / `environment` / `team` tag.
- **Forecast** — simple linear run-rate projection for the next month.

Each answer is grounded: the offline engine cites the dataset slice it used; the live agent cites the
Fabric tool/data it queried.

## Microsoft grounding (current docs)
- **Consume Fabric data agent from Foundry (preview)** — connection (workspace-id + artifact-id) →
  `FabricTool`/knowledge source; identity passthrough (OBO); same-tenant requirement; F2+ capacity.
- **Foundry MCP tool** — `RemoteTool` connection (`AgenticIdentityToken`) and local MCP via
  `McpClientFactory` (`StdioClientTransport`) → `ListToolsAsync()` → `AITool[]` on `AsAIAgent`.
- **Microsoft Agent Framework (C#)** — `AIProjectClient.AsAIAgent(model, instructions, name, tools)`,
  `RunAsync` / `RunStreamingAsync(message, session)`, `AgentSession` for multi-turn, `AgentRunOptions`.
- **Azure App Service + Foundry (.NET) tutorial** — App Service `.NET` hosting, `AIProjectClient`
  injected as a service, app exposed as an OpenAPI tool (`MapOpenApi`).

## Security
- System-assigned managed identity on the web app; **keyless** Foundry access (`disableLocalAuth`).
- Fabric data agent uses **user identity passthrough** (OBO) in live mode — each user only sees data
  they may access. (Service principal is not supported for the Fabric data agent; documented.)
- RBAC: Cognitive Services User + OpenAI User (Foundry), Storage Blob Data Contributor, Key Vault
  Secrets User. HTTPS-only, TLS 1.2+, FTPS disabled, no public blob access. Standard App Service plan.

## Trade-offs / future work
- Wire the live Fabric data agent end-to-end once a published Fabric data agent + F2 capacity exist
  (config-only: `Foundry:Enabled=true`, `Foundry:ProjectEndpoint`, `Fabric:ConnectionId`).
- Add an AG UI / CopilotKit or Teams frontend against the existing SSE contract.
- Move conversation + transcripts from in-memory/file to Redis/Cosmos/Blob for scale-out durability.
- Replace seeded dataset with a live OneLake/warehouse query path in the MCP server.
