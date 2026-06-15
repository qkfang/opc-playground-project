# proj39 — Microsoft Foundry Intake & Origination POC

A **.NET 10 Azure web app** that demonstrates an AI **Intake & Origination** workflow for B2B sales,
powered by a **Microsoft Foundry prompt agent** (Microsoft Agent Framework) with a deterministic
**offline fallback** so it runs anywhere — no Azure required for the demo.

> Mocked inbound email → structured **Lead / Account / Opportunity** extraction → early
> **triage & classification** → **Lead Management Agent** research + inbound demand signals →
> **Report Agent** origination study. End-to-end, in the browser.

---

## What it does (the 5 user-facing flows)

| # | Stage | What you see |
|---|-------|--------------|
| 1 | **Mock email intake** (trigger) | A mock inbox of realistic inbound enquiries. Pick one — or compose your own — to trigger the pipeline. |
| 2 | **Structured extraction** | The email is parsed into **Account**, **Lead**, and **Opportunity** records (CRM-style) with a confidence score. |
| 3 | **Early triage / classification** | Transparent, weighted scoring → **Hot / Warm / Cold / Spam**, with routing (Enterprise/Inside Sales/Nurture/Quarantine) and an SLA. Every point is shown. |
| 4 | **Lead research + demand signals** | A *Lead Management Agent* produces a company overview, captures **inbound demand signals** (strength-rated), competitors, and recommended actions. |
| 5 | **Report / study** | A *Report Agent* assembles an **origination study** (executive summary + sections), downloadable as markdown. |

Each stage is logged as an **agent step** in the UI so the multi-agent pipeline is observable, and each
step shows whether it ran on the **Foundry** agent or the **offline** engine.

---

## Architecture

```
Browser (Razor Pages + vanilla JS)
        │  fetch /api/...
        ▼
Minimal API  ──────────────────────────────────────────────┐
  /api/emails           mock inbox (trigger source)         │
  /api/cases/process/*  run the pipeline                    │
        │                                                   │
        ▼                                                   │
IOriginationEngine                                          │
  ├── FoundryOriginationEngine   (live: Microsoft Foundry prompt agent)
  │        AIProjectClient.AsAIAgent(...) → 4 grounded JSON calls
  │        (extraction → triage → research → report)
  │        falls back to ▼ on any error
  └── OfflineOriginationEngine   (deterministic rules/heuristics — always works)
        │
        ▼
OriginationCaseService  → persists cases to App_Data (or /home/site/data on App Service)
```

- **Engine selection** is automatic: if `Foundry:Enabled=true` and a `ProjectEndpoint` is set, the live
  agent is used; otherwise the offline engine. The Foundry engine *also* falls back to offline on any
  runtime failure, so a demo is never blocked by quota/auth/config.
- **Identity:** the App Service uses a **system-assigned managed identity**; Foundry, Storage, and Key
  Vault are accessed keyless via Entra ID (RBAC assigned in bicep). No secrets in app settings.

---

## Run locally

Prereqs: .NET 10 SDK.

```bash
cd proj39_foundry_intake_origination_poc/apps/web
dotnet run
# open the printed http://localhost:5xxx URL
```

The app starts in **offline** mode by default (`Foundry:Enabled=false`), so it's fully functional with
no Azure. Pick an email and click **Run intake & origination pipeline**.

### Smoke test

```powershell
pwsh proj39_foundry_intake_origination_poc/scripts/smoke.ps1
```

Builds, boots the app, and runs 8 end-to-end API checks (health, inbox, Hot classification, budget-vs-
revenue extraction, spam quarantine, report download, ad-hoc compose, case listing).

### Unit + API tests

```bash
dotnet test proj39_foundry_intake_origination_poc/tests/Proj39.IntakeOrigination.Tests.csproj
```

12 tests (offline-engine unit tests + `WebApplicationFactory` API integration tests).

---

## Enable the live Foundry agent

Set these (user-secrets locally, or app settings in Azure):

```
Foundry__Enabled=true
Foundry__ProjectEndpoint=https://<your-ais>.services.ai.azure.com/api/projects/<project>
Foundry__ModelDeploymentName=gpt-4o
```

Locally you authenticate with `az login` (DefaultAzureCredential); in Azure the managed identity is used.

---

## Deploy to Azure

Two manual GitHub Actions workflows (match the rest of this repo; require the `AZURE_CREDENTIALS` secret):

1. **`proj39_foundry_intake_origination_infra`** — provisions App Service (Linux, .NET 10), Microsoft
   Foundry (AI Services account + project + `gpt-4o` deployment), Storage, Key Vault, Log Analytics /
   App Insights, and the managed-identity RBAC.
2. **`proj39_foundry_intake_origination_deploy`** — builds, publishes, zip-deploys the app, and
   smoke-tests `/api/health`.

Infra is defined in [`bicep/`](./bicep) (`main.bicep` + modules). Default region `australiaeast`,
App Service SKU `S1`.

---

## API surface (also an OpenAPI tool)

`/openapi/v1.json` is served (the same document can be registered as a Foundry Agent Service OpenAPI tool).

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/health` | Liveness + engine mode |
| GET | `/api/emails` | List mock inbound emails |
| GET | `/api/emails/{id}` | Get one email |
| POST | `/api/emails` | Add a custom inbound email |
| POST | `/api/cases/process/{emailId}` | Run pipeline on a mock email |
| POST | `/api/cases/process` | Run pipeline on an ad-hoc email payload |
| GET | `/api/cases` | List processed cases |
| GET | `/api/cases/{caseId}` | Full case (extraction/triage/research/report) |
| GET | `/api/cases/{caseId}/report` | Download the origination study (markdown) |

---

## Project layout

```
proj39_foundry_intake_origination_poc/
├── apps/web/                     # .NET 10 Razor Pages + minimal API
│   ├── Models/                   # domain records + options
│   ├── Services/                 # engines (offline + Foundry), mock inbox, case service
│   │   └── Foundry/              # FoundryOriginationEngine (Agent Framework)
│   ├── Pages/                    # Razor UI
│   ├── wwwroot/                  # css + js
│   └── Data/mock-emails.json     # seeded trigger emails
├── bicep/                        # main.bicep + modules (App Service, Foundry, Storage, KV, monitoring)
├── tests/                        # xUnit unit + API integration tests
├── scripts/smoke.ps1             # local end-to-end smoke test
├── samples/                      # example generated origination studies
└── docs/                         # solution / task / todo
```

> POC scope: mocked inbound email and mocked external research signals. Extraction/triage rules are
> heuristic and editable; not for production decisions without validation.
