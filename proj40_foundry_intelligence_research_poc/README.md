# proj40 — Foundry Intelligence & Research POC

A .NET 10 Azure web app that demonstrates an **Intelligence & Research** multi-agent workflow built on
the **Microsoft Foundry** prompt-agent pattern (per `qkfang/template-repo-agent`). It receives a
customer document via a mock email inbox tray, extracts key entities, generates insights, pulls
mocked internal/external sources keyed by those entities, drafts a research brief, and composes a
send-ready report email summarising the insights.

It runs **fully offline by default** (deterministic mock engine) so it is demonstrable with zero Azure
config, and transparently upgrades to the **live Foundry prompt agent** when configured — falling back
to offline on any runtime failure so the POC is never blocked.

---

## The five flows (distinct tabs)

| # | Flow | Where |
|---|------|-------|
| 1 | **Inbox intake** — customer document arrives via a mock email tray (with attachments) | Left inbox panel + reader |
| 2 | **Insights** — generate insights from the customer email **and** its document | Tab 2 · Insights |
| 3 | **Source intelligence** — pull data from mocked **internal + external** sources, keyed by the document's key entities | Tab 3 · Sources |
| 4 | **Research Agent** — synthesise a research brief (summary, findings, risks, opportunities, citations) | Tab 4 · Research brief |
| 5 | **Report email** — compose a send-ready email that summarises the insights for the right stakeholder | Tab 5 · Report email |

Tab 1 (Entities) exposes the extracted entities that anchor the whole pipeline; every stage is logged
in an **agent trace** for traceability.

---

## Architecture

```
 Mock inbox (Data/mock-emails.json, each email + attached CustomerDocument)
        │
        ▼
 ┌──────────────────────────────────────────────────────────────────────┐
 │ IResearchEngine                                                        │
 │   FoundryResearchEngine  (live: AIProjectClient.AsAIAgent, JSON stages)│
 │     └─ falls back to ─▶ OfflineResearchEngine (deterministic, default) │
 └──────────────────────────────────────────────────────────────────────┘
   1. ExtractedEntities   (people / orgs / topics / tech / amounts / dates)
   2. Insights            (Need / Risk / Opportunity / Context / Signal + evidence)
   3. SourceHits          (SourceCorpus.Pull(entities) → internal + external, cited)
   4. ResearchBrief       (exec summary, findings, risks, opportunities, citations)
   5. ReportEmail         (To/Subject/body + rendered .md, routed by industry)
        │
        ▼
 ResearchCaseService  →  App_Data/cases/*.json   (POC persistence; Blob-ready)
```

- **UI:** Razor Pages, a two-panel inbox/reader with a 5-stage tabbed result view (vanilla JS, no build step).
- **API:** minimal API + OpenAPI document (`/openapi/v1.json`).
- **Telemetry:** Application Insights.

---

## Run locally (offline, no Azure)

```bash
cd proj40_foundry_intelligence_research_poc/apps/web
dotnet run
# open the printed http://localhost:5xxx, pick an email, click
# "Run intelligence & research pipeline". Try "+ Compose" for an ad-hoc email + pasted document.
```

The engine badge (top-right) shows **Offline (mock)** until Foundry is configured.

### Tests & smoke

```bash
# Unit + API integration tests (15)
dotnet test proj40_foundry_intelligence_research_poc/tests/Proj40.IntelligenceResearch.Tests.csproj -c Release

# End-to-end local smoke (boots the app, exercises the API; 9 checks)
pwsh proj40_foundry_intelligence_research_poc/scripts/smoke.ps1
```

---

## Enable the live Foundry agent

Set these (env vars or `appsettings`) and the app uses the live prompt agent automatically:

```
Foundry__Enabled=true
Foundry__ProjectEndpoint=https://<resource>.services.ai.azure.com/api/projects/<project>
Foundry__ModelDeploymentName=gpt-4o
```

Auth is **keyless** — `DefaultAzureCredential` (the App Service managed identity in Azure, or your
`az login` locally). On any failure the pipeline falls back to the offline engine and records why.

---

## Deploy to Azure

Two manual GitHub Actions workflows (require the `AZURE_CREDENTIALS` secret):

1. **`proj40_foundry_intelligence_research_infra`** — provisions App Service (Linux, .NET 10, S1),
   Microsoft Foundry (AI Services + project + `gpt-4o`), Storage (keyless), Key Vault, Log Analytics /
   App Insights, and managed-identity RBAC (Bicep in `bicep/`).
2. **`proj40_foundry_intelligence_research_deploy`** — builds, publishes, and zip-deploys the app, then
   health-checks `/api/health`.

Resource group: `rg-playground-01`, region `australiaeast`, all resources `proj40`-prefixed.

---

## API surface

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/health` | Health + active engine (offline/foundry) |
| GET | `/api/inbox` | List mock emails (with attachment metadata) |
| GET | `/api/inbox/{id}` | One email + full document content |
| POST | `/api/process/{id}` | Run the full pipeline for a mock email |
| POST | `/api/process` | Run the pipeline for an ad-hoc email + pasted document |
| GET | `/api/cases` | List recent processed cases |
| GET | `/api/cases/{id}` | Retrieve a persisted case |
| GET | `/api/cases/{id}/report` | Download the report email as `text/markdown` |

---

## Project layout

```
proj40_foundry_intelligence_research_poc/
├── apps/web/
│   ├── Models/            IntelligenceModels.cs, Options.cs
│   ├── Services/          OfflineResearchEngine.*.cs, SourceCorpus.cs, MockEmailStore.cs,
│   │   └── Foundry/       FoundryResearchEngine.cs            ResearchCaseService.cs
│   ├── Pages/             Index.cshtml (+ .cs), Shared/_Layout, Error
│   ├── wwwroot/           css/site.css, js/app.js
│   ├── Data/              mock-emails.json, source-corpus.json
│   └── Program.cs         DI + minimal API + engine selection
├── bicep/                 main.bicep + main.bicepparam + modules/{appservice,foundry,storage,keyvault,monitoring}
├── scripts/smoke.ps1      end-to-end local smoke (9 checks)
├── tests/                 xUnit (7 unit + 8 API integration)
├── samples/               example research briefs / report emails
└── docs/                  solution.md, task.md, todo.md
```

---

## POC scope

Inbound emails and customer documents are **mocked** (`Data/mock-emails.json`); the internal/external
research corpus is **mocked** (`Data/source-corpus.json`) and keyed by entity — the exact seam where
real connectors (CRM, news, filings, Azure AI Search) plug in without changing the pipeline shape.
Demo/mock data — not for production decisions.
