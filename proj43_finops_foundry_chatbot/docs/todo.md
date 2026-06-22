# proj43 — FinOps Foundry Chatbot — TODO

## Build (toad)
- [x] Study proj37 Foundry pattern + research Fabric data agent / MCP wiring
- [x] Scaffold project structure (apps/web, bicep, docs, scripts, tests)
- [x] Write solution.md / task.md / todo.md
- [x] Core models + options (Foundry, Fabric, MCP, Storage, Chat)
- [x] FinOps seeded dataset + analytics (deterministic engine)
- [x] OfflineFinOpsAgent (grounded conversational answers + streaming)
- [x] FoundryFinOpsAgent (Agent Framework, AgentSession multi-turn, Fabric tool + MCP tools, offline fallback)
- [x] ConversationStore (multi-turn session memory)
- [x] Minimal API: /api/chat (sync) + /api/chat/stream (SSE) + /api/health + /api/suggestions
- [x] Razor chat UI (server-rendered) + CSS + vanilla SSE chat client
- [x] Markdown rendering (Markdig) for assistant/tool tables
- [x] Program.cs wiring (DI, engine selection, telemetry, OpenAPI)
- [x] Unit/integration tests (analytics, offline agent, API endpoints)
- [x] smoke.ps1 (build + health + chat round-trip)
- [x] Bicep (main + appservice/foundry/storage/keyvault/monitoring) + RBAC
- [x] GitHub Actions: proj43_finops_foundry_chatbot_infra.yml + _deploy.yml
- [x] README
- [x] Local build + test + smoke green; capture evidence in shared-context/projects/proj43.md
- [x] Handoff to toadette (QA) with new task_id

## QA (toadette)
- [ ] Build, test, smoke; exercise chat in browser; capture screenshot; PASS → yoshi

## Deploy (yoshi)
- [ ] Bicep to rg-playground-01 + app via GitHub Actions; verify; screenshot
