# proj44 — APRA CPS 230 Compliance-Mapping POC

A serious enterprise **.NET 10** Microsoft **Foundry** proof-of-concept that turns an APRA prudential
standard into a fully traceable compliance framework using a **six-agent pipeline**, then analyses the
**requirement → policy → standard → control** spine for coverage and gaps.

It mirrors the proj37 Foundry blueprint exactly (packages, DI, Foundry gating, bicep, smoke, workflows)
and ships a **deterministic offline engine** so the whole framework — **130 policies, 35 controls** and
every mapping — is produced with **no live Azure**. Build, test and smoke all pass on a bare VM.

- **Corpus:** APRA Prudential Standard **CPS 230 Operational Risk Management** (effective 1 July 2025).
  See `apps/web/Data/cps230-source.md` and `docs/solution.md` for why CPS 230 was chosen.
- **Namespace / assembly:** `Proj44.Compliance.Web`.

## The six agents (pipeline)
| # | Stage key | Agent | Produces |
|---|-----------|-------|----------|
| 1 | `ingestion` | CPS Ingestion Agent | Clean clauses parsed from the CPS document |
| 2 | `requirements` | Requirement Extraction Agent | Structured regulatory requirements |
| 3 | `policies` | Policy Authoring Agent | The policy framework (130 policies) |
| 4 | `standards` | Standard Authoring Agent | Implementation standards + policy→standard mapping |
| 5 | `controls` | Control Authoring Agent | The control library (35) + standard→control mapping |
| 6 | `gap` | Gap & Traceability Agent | Coverage %, orphans and broken chains |

When Foundry is configured (`Foundry:Enabled=true` + `Foundry:ProjectEndpoint`), the
`FoundryComplianceEngine` orchestrates the six agents with `Microsoft.Agents.AI` /
`Microsoft.Agents.AI.Foundry` and **falls back to the offline engine on any failure** (recording the
reason in the agent transcript). The seeded framework — not the model — guarantees the counts.

## UI tabs (Razor Pages, persistent top nav)
Overview · Requirements · Policies · Standards · Controls · Mappings (relationship matrix) ·
Gap Analysis · Traceability (drill requirement→policy→standard→control) · Agents / Pipeline.
Each tab carries an agent badge and an **agent-instructions** popup (`GET /api/agent-instructions`).

## API (minimal APIs under `/api`)
`GET /api/health` · `GET /api/agent-instructions` · `GET /api/framework` ·
`GET /api/requirements|policies|standards|controls|clauses` · `GET /api/gaps` ·
`GET /api/traceability/{requirementId}` · `POST /api/run` · OpenAPI at `/openapi/v1.json`.

## Run it locally
```pwsh
cd proj44_apra_cps_compliance_poc/apps/web
dotnet run -c Release
# then browse http://localhost:5xxx  (see console)  — or hit /api/framework
```

## Gates
```pwsh
# Build (0 warnings / 0 errors)
dotnet build proj44_apra_cps_compliance_poc/apps/web -c Release

# Tests (xUnit + WebApplicationFactory, offline engine)
dotnet test proj44_apra_cps_compliance_poc/tests -c Release

# End-to-end smoke (builds, starts on :5244, exercises the API + a UI tab, exits 0)
pwsh proj44_apra_cps_compliance_poc/scripts/smoke.ps1
```

## Deploy (Azure)
- `bicep/main.bicep` (+ `main.bicepparam`, `modules/*`) provisions App Service, Foundry (AI Services
  account + project + model deployment), Storage, Key Vault, monitoring and managed-identity RBAC.
- GitHub Actions: `proj44_apra_cps_compliance_infra` then `proj44_apra_cps_compliance_deploy`
  (resource group `rg-playground-01`, `australiaeast`).

See `docs/solution.md` for architecture, `docs/task.md` for the brief, `docs/todo.md` for status.

> CPS 230 content here is a faithful paraphrase/summary of APRA's standard for demonstration; it is
> not verbatim and is not legal advice.
