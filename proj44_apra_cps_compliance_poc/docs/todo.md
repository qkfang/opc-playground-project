# Todo / Status — proj44 APRA CPS 230 Compliance-Mapping POC

Status: **COMPLETE** — all three gates green on a VM with no Foundry/Azure.

## Checklist
- [x] .NET 10 Razor Pages web app, namespace/assembly `Proj44.Compliance.Web`.
- [x] Six agent-backed pipeline stages with per-stage persona/instructions + `AgentStepLog`
      (ingestion, requirements, policies, standards, controls, gap).
- [x] Foundry pattern mirrors proj37 (Microsoft.Agents.AI[.Foundry] 1.5.0, Azure.Identity 1.21.0,
      `AsAIAgent`, `DefaultAzureCredential`, Foundry/Storage gating, gpt-4o default, per-stage AgentName,
      NoWarn MEAI001;OPENAI001;AOAI001).
- [x] Deterministic offline engine builds the full framework (130 policies, 35 controls, all mappings,
      gaps) with no Azure; Foundry path falls back to offline on any failure.
- [x] 9 UI tabs + persistent top nav + per-tab agent badge + agent-instructions popup + Pipeline view.
- [x] CPS 230 corpus + themed requirements + 130 policies + 38 standards + 35 controls, fully mapped,
      with deliberate gaps. Seed ships via `<Content Include="Data\**">`.
- [x] Gap detection (orphans + coverage %) in a tab and `GET /api/gaps`.
- [x] API surface under `/api` + OpenAPI; `public partial class Program {}` for tests.
- [x] xUnit tests (mirrors proj37 tests csproj).
- [x] scripts/smoke.ps1 (mirrors proj37 structure; port 5244).
- [x] bicep main + bicepparam + modules/{appservice,foundry,keyvault,monitoring,storage}.bicep (proj44).
- [x] Two GH workflows at repo `.github/workflows` (infra + deploy).
- [x] docs (solution/task/todo) + README.
- [x] Working tree left uncommitted; no bin/obj/temp committed.

## Captured gate results (Release)
- **Build:** `dotnet build -c Release` → **0 warnings / 0 errors**.
- **Tests:** `dotnet test -c Release` → **39 passed / 0 failed / 0 skipped** (3 focused test classes:
  `ApiTests` end-to-end via WebApplicationFactory, `FrameworkTests` deterministic graph contract,
  `PipelineTests` six-stage agent ordering).
- **Smoke:** `pwsh scripts/smoke.ps1` → **exit 0**, all 9 checks green:
  health(offline) · agent-instructions(6 stages) · run(6 ordered steps) · framework(≥130/≥30 + mapped) ·
  gaps(findings + coverage) · traceability good-chain · traceability broken-chain · policies/controls
  lists · UI tabs render 200 with nav.

## Seed counts produced (deterministic)
| Entity | Count |
|--------|-------|
| Clauses | 10 |
| Requirements | 37 (REQ-001..REQ-037) |
| Policies | 130 (POL-001..POL-130) |
| Standards | 38 (STD-001..STD-038) |
| Controls | 35 (CTL-001..CTL-035) |
| Requirement→Policy links | 105 |
| Policy→Standard links | 254 |
| Standard→Control links | 73 |
| Total gaps | 7 |

Coverage: requirement→policy **94.6%**, policy→standard **97.7%**, standard→control **94.7%**,
control referenced **100%**, end-to-end **89.2%**.

Deliberate gaps: requirements REQ-034, REQ-035 (no policy); policies POL-128, POL-129, POL-130 (no
standard); standards STD-037, STD-038 (no control); orphan controls: none.

## Pipeline agents (and that /api/run logs all six, in order)
1. ingestion — CPS Ingestion Agent
2. requirements — Requirement Extraction Agent
3. policies — Policy Authoring Agent
4. standards — Standard Authoring Agent
5. controls — Control Authoring Agent
6. gap — Gap & Traceability Agent

## Follow-ups / nice-to-haves (not required by the brief)
- Export the framework to XLSX/CSV (the storage container is already provisioned).
- Live Foundry run captured against a real project endpoint (currently exercised via offline + fallback).
- Per-theme coverage heatmap on the Mappings tab.
