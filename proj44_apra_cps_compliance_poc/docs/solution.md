# Solution — proj44 APRA CPS 230 Compliance-Mapping POC

## Why CPS 230
The brief asked for a regulatory compliance-mapping corpus at meaningful scale (~130 policies, ≥30
controls, full requirement→policy→standard→control traceability with deliberate gaps). **APRA
Prudential Standard CPS 230 Operational Risk Management** (effective 1 July 2025) is an excellent fit:

- It is a current, high-profile Australian prudential standard that consolidates CPS 231 (Outsourcing)
  and CPS 232 (Business Continuity Management).
- Its obligations decompose naturally into ~10 themes (governance/board accountability, operational
  risk management framework, operational risk controls, business/operational resilience incl.
  tolerance levels, critical operations, service-provider/material-arrangement management, incident &
  disruption management, business continuity plan, scenario/severe-but-plausible testing, and
  notifications to APRA) — ideal for a layered policy/standard/control framework.
- It is rich enough to justify 130 policies and 35 controls without padding.

The paraphrased source corpus the Ingestion agent parses is at
`apps/web/Data/cps230-source.md` (ships via `<Content Include="Data\**">`). Content is summarised, not
verbatim, and is not legal advice.

## Architecture
```
            ┌────────────────────────── Razor Pages UI (9 tabs, top nav) ──────────────────────────┐
            │ Overview · Requirements · Policies · Standards · Controls · Mappings · Gaps ·         │
            │ Traceability · Agents/Pipeline      (wwwroot/js/app.js renders from /api)             │
            └───────────────▲──────────────────────────────────────────────────────────────────────┘
                            │ fetch
   ┌────────────────────────┴───────────── minimal APIs under /api ───────────────────────────────┐
   │ health · agent-instructions · framework · requirements/policies/standards/controls/clauses ·  │
   │ gaps · traceability/{id} · run (POST)                                                          │
   └───────────────▲───────────────────────────────────────────────────────────────────────────────┘
                   │ IComplianceEngine.BuildAsync()
        ┌──────────┴───────────┐   configured?              ┌──────────────────────────────────────┐
        │ FoundryComplianceEngine ──── yes ───▶ 6 Foundry agents (AsAIAgent) ── any failure ─┐      │
        │                       │                                                            ▼      │
        │ OfflineComplianceEngine ◀─── no / fallback ───────────────── deterministic FrameworkBuilder│
        └──────────────────────────────────────────────────────────────────────────────────────────┘
                                              │ builds
                  RegulatorySource + Clauses + Requirements + Policies + Standards + Controls + mappings
                                              │
                       GapAnalyzer (coverage % + orphans)   TraceabilityResolver (per-requirement chain)
```

### Foundry pattern (mirrors proj37)
- Packages: `Microsoft.Agents.AI` 1.5.0, `Microsoft.Agents.AI.Foundry` 1.5.0, `Azure.Identity` 1.21.0;
  `NoWarn $(NoWarn);MEAI001;OPENAI001;AOAI001`.
- `FoundryOptions` / `StorageOptions` bound from config sections `Foundry` / `Storage`;
  `IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ProjectEndpoint)`.
- Engine selection in `Program.cs`: if `FoundryOptions.IsConfigured` register `FoundryComplianceEngine`,
  else `OfflineComplianceEngine`.
- Each stage is its own logical agent: `client.GetAIProjectClient()...AsAIAgent(model, instructions,
  name)` with a per-stage persona from `AgentInstructions`, an `agent.RunAsync(prompt, ct)` call, and an
  `AgentStepLog` entry (step/agent/summary) so the UI shows which agent did what. Any exception →
  offline fallback with a recorded reason.

### Determinism (the important part)
The counts are guaranteed by `FrameworkBuilder` (split into partial files for ingestion, requirements,
policies, standards, controls and mappings), **not** by the model:

- `FrameworkBuilder.Requirements.cs` — 37 requirements (REQ-001..REQ-037) across the CPS 230 themes.
- `FrameworkBuilder.Policies.cs` — 130 policies (POL-001..POL-130) across policy domains.
- `FrameworkBuilder.Standards.cs` — 38 implementation standards (STD-001..STD-038).
- `FrameworkBuilder.Controls.cs` — 35 controls (CTL-001..CTL-035), each preventive/detective/corrective.
- `FrameworkBuilder.Mappings.cs` — theme-aligned wiring with coverage guarantees: every non-gap
  requirement gets ≥1 policy, every non-gap policy ≥1 standard, every non-gap standard ≥1 control, and
  every control is referenced by ≥1 standard.

### Deliberate gaps (so Gap Analysis has real findings)
Defined in `FrameworkBuilder.KnownGaps`:
- **Requirements with no policy:** REQ-034, REQ-035.
- **Policies with no standard:** POL-128, POL-129, POL-130.
- **Standards with no control:** STD-037, STD-038.
- Orphan controls: none (full control coverage by design).

`GapAnalyzer` reports orphans at each layer plus coverage percentages; `TraceabilityResolver` returns the
full chain for a requirement and flags the first broken link.

## Produced framework (offline, deterministic)
| Entity | Count |
|--------|-------|
| Clauses (CPS 230 sections) | 10 |
| Requirements | 37 |
| Policies | 130 |
| Standards | 38 |
| Controls | 35 |
| Requirement→Policy links | 105 |
| Policy→Standard links | 254 |
| Standard→Control links | 73 |
| Total gaps | 7 |

Coverage: requirement→policy 94.6%, policy→standard 97.7%, standard→control 94.7%, control referenced
100%, end-to-end 89.2%.

## Tech / conventions
- .NET 10, `Microsoft.NET.Sdk.Web`, Razor Pages + minimal APIs, OpenAPI via
  `Microsoft.AspNetCore.OpenApi` 10.0.9, `Program` exposed as `public partial class Program {}` for
  `WebApplicationFactory` tests.
- Tests: xUnit 2.9.2, `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (mirrors proj37 tests csproj).
- IaC: bicep main + `modules/{appservice,foundry,keyvault,monitoring,storage}.bicep`, baseName `proj44`,
  `Foundry__AgentName=proj44-compliance`, container `compliance`, `australiaeast`, RG `rg-playground-01`.
- CI: `proj44_apra_cps_compliance_infra.yml` + `proj44_apra_cps_compliance_deploy.yml`.

## Deviations from the proj37 blueprint
- Domain is compliance mapping, not cost estimation, so models/services/pages/endpoints are
  domain-specific (requirements/policies/standards/controls/gaps/traceability vs scope/requirements/cost
  /workbook). The **shape** (offline-deterministic engine + Foundry fallback + AgentStepLog +
  agent-instructions popup + per-step tabs + minimal APIs + tests/smoke/bicep/workflows) is identical.
- Six pipeline agents instead of three (the brief requires ingestion → requirements → policies →
  standards → controls → gap).
- `FrameworkBuilder` is split across partial class files purely to keep each source file a reasonable
  size; it is one logical builder.
- The Foundry app settings drop proj37's `Foundry__UseFileSearch` (no file-search step in this POC) and
  add no new secrets; storage container default is `compliance` instead of `estimations`.
