# proj43 — FinOps Foundry Chatbot

An enterprise **.NET 10** web app that hosts a **conversational FinOps assistant**. The assistant is a
**Microsoft Foundry agent** (Microsoft Agent Framework) that can query governed **Microsoft Fabric**
cost & usage data as a tool — via the **Fabric data agent** connection and/or a **Fabric MCP** server —
to answer cloud cost questions in chat: spend, trends, top drivers, anomalies, commitment coverage,
showback and savings.

> Project code: `proj43_finops_foundry_chatbot` · part of the `opc-playground-project` repo.

## Why this design (chat UI: AG UI evaluated)
We evaluated **AG UI / CopilotKit** (TypeScript/React event-protocol chat). It fits React SPAs with an
AG-UI-protocol backend; bolting a React/Node build chain + protocol bridge onto a server-rendered .NET
app would add a second runtime and deploy artifact for a single chat surface. We chose a
**server-rendered .NET chat UI** (Razor + a small no-build vanilla JS client streaming over **SSE**):
one deployable artifact, trivially testable, enterprise-grade. The `/api/chat/stream` SSE contract is
exactly what an AG UI/CopilotKit, Teams, or React shell could consume later — so the option isn't
burned. Full rationale: [`docs/solution.md`](docs/solution.md).

## Two engines behind one interface (`IFinOpsAgent`)
1. **`FoundryFinOpsAgent`** (live) — `AIProjectClient.AsAIAgent(...)`, multi-turn `AgentSession`,
   streaming `RunStreamingAsync`. Fabric data access two ways:
   - **Fabric data agent tool** via Foundry connection id (`Fabric:ConnectionId`) — identity
     passthrough (OBO).
   - **Fabric MCP tools** via `McpClient` → `ListToolsAsync()` → `AITool[]` (`Mcp:*`).
   Falls back to the offline engine on any runtime failure, so chat never dead-ends.
2. **`OfflineFinOpsAgent`** (default) — deterministic intent routing → `FinOpsAnalytics` over a seeded
   12-month FinOps dataset → grounded Markdown answers (streamed). Always available; powers tests/CI/demo.

Engine selection is automatic: live when `Foundry:Enabled` + endpoint set, else offline.

## Run locally
```bash
cd apps/web
dotnet run
# open the printed http://localhost:5xxx  (engine badge shows "offline" with no Foundry config)
```

Ask things like: *"What did we spend last month?"*, *"Top 5 services by cost"*, *"Any cost anomalies?"*,
*"How is our commitment coverage?"*, *"Where can we save money?"*, *"Break down cost by team"*,
*"Forecast next month"*.

## Test + smoke
```bash
dotnet test                      # 29 tests: analytics, offline-agent intents, API + SSE
pwsh ./scripts/smoke.ps1         # release build + run + health + chat round-trip + SSE
```

## Configuration (`appsettings.json` / App Service settings)
| Key | Meaning |
| --- | --- |
| `Foundry:Enabled` | Use the live Foundry agent (else offline engine). |
| `Foundry:ProjectEndpoint` | Foundry project endpoint (AI Foundry API). |
| `Foundry:ModelDeploymentName` | Orchestration model (e.g. `gpt-4o-mini`). |
| `Fabric:ConnectionId` | Foundry connection id for the published Fabric data agent (enables Fabric tool). |
| `Mcp:Enabled` / `Mcp:Command` / `Mcp:Args` | Local stdio Fabric MCP server to attach as tools. |
| `Chat:MaxHistoryTurns` | Conversation context window. |
| `Storage:AccountUrl` / `Storage:LocalDataFolder` | Transcript persistence (Blob or local). |

## API
- `GET /api/health` — status + active engine + config flags + data freshness.
- `GET /api/suggestions` — starter FinOps prompts.
- `POST /api/chat` — `{ conversationId?, message }` → full reply (text + safe HTML).
- `POST /api/chat/stream` — same input → **SSE** (`meta` → `status` → `token`… → `done`).
- `GET /openapi/v1.json` — OpenAPI document.

## Deploy (Azure)
Infra + app via GitHub Actions (manual dispatch), mirroring the repo conventions
(`rg-playground-01`, `AZURE_CREDENTIALS`, `azure/login@v2`):
1. **`proj43_finops_foundry_chatbot_infra`** — Bicep: App Service (Linux, .NET 10), Microsoft Foundry
   (AI Services account + project + model deployment), Storage, Key Vault, Log Analytics/App Insights,
   managed-identity RBAC. Optional `fabricConnectionId` input.
2. **`proj43_finops_foundry_chatbot_deploy`** — build, publish, zip-deploy; health smoke.

Infra is **keyless** (managed identity, `disableLocalAuth`); the Fabric data agent uses user identity
passthrough in live mode.

## Layout
```
apps/web/        .NET 10 web app (Razor UI + minimal API + agents/analytics)
bicep/           main + modules (appservice, foundry, storage, keyvault, monitoring)
docs/            solution.md, task.md, todo.md
scripts/         smoke.ps1
tests/           xUnit (analytics, offline agent, API/SSE)
.github/workflows/ proj43_finops_foundry_chatbot_{infra,deploy}.yml
```

## Notes / future work
- Wire the live Fabric data agent end-to-end once a published Fabric data agent + F2 capacity exist
  (config-only). Replace the seeded dataset with a live OneLake/warehouse query in the MCP server.
- Add an AG UI/CopilotKit or Teams frontend against the existing SSE contract.
- Move conversation/transcripts to Redis/Cosmos/Blob for scale-out durability.

Figures are directional, grounded in governed Fabric data (offline sample when Foundry is unconfigured).
