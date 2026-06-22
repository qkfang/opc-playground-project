# proj43 — Task Breakdown

## Component A — Domain + Data (deterministic core)
- `Models/FinOpsModels.cs` — CostRecord, ServiceSpend, anomaly, recommendation, chat DTOs.
- `Models/Options.cs` — FoundryOptions, FabricOptions, McpOptions, StorageOptions, ChatOptions.
- `Services/FinOpsDataset.cs` — seeded 12-month cost/usage dataset (deterministic, RNG-seeded).
- `Services/FinOpsAnalytics.cs` — spend/trend/top-N/anomaly/coverage/forecast/optimisation math.

## Component B — Conversational agents
- `Services/IFinOpsAgent.cs` — streaming chat abstraction.
- `Services/OfflineFinOpsAgent.cs` — intent routing → analytics → grounded Markdown answer (streamed).
- `Services/Foundry/FoundryFinOpsAgent.cs` — Agent Framework agent, AgentSession multi-turn,
  Fabric data agent tool + MCP tools, transparent offline fallback.
- `Services/ConversationStore.cs` — per-session history (sliding window).
- `Services/AgentPersona.cs` — system persona + suggested prompts.

## Component C — Web surface
- `Program.cs` — DI, engine selection, telemetry, OpenAPI, chat endpoints (sync + SSE).
- `Pages/Index.cshtml(.cs)` — chat UI shell (server-rendered).
- `Pages/Shared/_Layout.cshtml` + `wwwroot/css/site.css` + `wwwroot/js/chat.js` — enterprise chat UX.
- `Services/MarkdownRenderer.cs` — safe Markdig HTML for tables.

## Component D — Tests + Infra
- `tests/*` — FinOpsAnalyticsTests, OfflineFinOpsAgentTests, ChatApiTests (WebApplicationFactory).
- `scripts/smoke.ps1` — release build + run + /api/health + chat round-trip assertions.
- `bicep/*` — main + 5 modules, managed identity + RBAC, Standard App Service plan.
- `.github/workflows/proj43_finops_foundry_chatbot_{infra,deploy}.yml`.

## Acceptance (done_when)
- New project folder with app + docs + infra. ✔ build/test/smoke green locally. ✔ Foundry + Fabric MCP
  path implemented/scaffolded. ✔ Chat UI evaluated (AG UI justified). ✔ evidence in proj43.md.
  ✔ QA handoff to toadette with new task_id.
