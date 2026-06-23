# Task — proj44 APRA CPS 230 Compliance-Mapping POC

## Brief (as received)
Build a serious enterprise .NET 10 Microsoft Foundry POC that mirrors the proj37 blueprint exactly and
maps an APRA prudential standard into a fully traceable compliance framework via a six-agent pipeline.

### Done-when
1. .NET 10 Razor Pages web app under `apps/web`, namespace/assembly `Proj44.Compliance.Web`.
2. A real multi-agent Foundry design with six separate agent-backed stages, each with its own
   persona/instructions and an `AgentStepLog` entry:
   ingestion → requirement identification → policy authoring → standard authoring → control authoring →
   gap/traceability analysis. Same Foundry pattern as proj37 (Microsoft.Agents.AI[.Foundry] 1.5.0,
   Azure.Identity 1.21.0, `AsAIAgent`, `DefaultAzureCredential`, Foundry/Storage options with
   Enabled + ProjectEndpoint gating, model default gpt-4o, per-stage AgentName, NoWarn experimental).
   A deterministic OFFLINE engine must produce the full framework (≥130 policies, ≥30 controls, all
   mappings, gaps) with no live Azure; Foundry path falls back to offline on any failure.
3. Distinct tabs/pages per workflow step + traceability views, persistent top nav, agent stages visibly
   listed (pipeline view + per-tab badge + agent-instructions popup).
4. Seed/demo data: CPS 230 corpus, realistic requirement set across the standard's themes, ~130
   policies, ~30–50 standards, ≥30 controls, every layer mapped, with a small number of deliberate gaps.
   Seed ships under `apps/web/Data` via `<Content Include>`.
5. Gap detection across requirement→policy→standard→control (orphans + coverage %), surfaced in a tab
   and an `/api` endpoint.
6. docs/ (solution/task/todo), README, tests/ (xUnit, mirrors proj37 tests csproj), scripts/smoke.ps1
   (mirrors proj37 structure), bicep/ (main + bicepparam + 5 modules), two GH workflows
   (infra + deploy) at repo `.github/workflows`, RG rg-playground-01, australiaeast.
7. API surface under `/api` (health, agent-instructions, framework, requirements, policies, standards,
   controls, gaps, traceability/{id}, run) + OpenAPI; `Program` partial for tests.

### Gates (Release)
- `dotnet build -c Release` → 0 warnings / 0 errors.
- `dotnet test -c Release` → all green, meaningful assertions (≥130 policies & ≥30 controls; mappings
  hold except gap fixtures; gap analysis finds known orphans; traceability full chain for a good
  requirement; health 200; run offline logs 6 ordered steps; agent-instructions returns 6 stages).
- `pwsh scripts/smoke.ps1` → exit 0, all checks green (health, run with 6 steps, framework ≥130/≥30,
  gaps findings, traceability chain, a tab renders 200 with the nav).

### Constraints
- Source-only buildable, net10.0, suppress experimental warnings. No fabricated evidence.
- CPS 230 content substantively accurate (paraphrase, not long verbatim).
- Leave the working tree UNCOMMITTED; no git commit/push, no publish, no bin/obj in git.
- Touch no other proj* folder except reading proj37 for reference.

## Outcome
All gates pass on a VM with no Foundry/Azure. See `docs/todo.md` for the exact captured numbers.
