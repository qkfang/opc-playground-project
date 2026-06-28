# proj45 — Relay Desk · Inbound Email Orchestration (Microsoft Foundry enterprise POC)

A .NET 10 enterprise POC that watches an inbound support mailbox and drives each message through a
five-stage agent pipeline: **Email → Triage → Intent → Task (D365) → Outcome**. Every stage is backed
by an explicit **Microsoft Foundry** agent instruction set (surfaced in the UI), with a deterministic
**offline engine** as a per-stage fallback so the demo always runs with zero Azure connectivity.

> Sibling of the prior Foundry enterprise POCs (esp. `proj41` underwriting). Same architecture —
> net10.0 Razor Pages SPA, `Microsoft.Agents.AI.Foundry`, keyless managed-identity Foundry, Standard
> App Service, offline fallback — but this is a **new email triage + intent orchestration** demo.

## The five stages (each = one Foundry agent)

| # | Page    | Agent                              | What it does |
|---|---------|------------------------------------|--------------|
| 1 | Email   | `relay-triage-extraction-agent`    | Watches the mailbox; extracts sender, entities, order/invoice refs, account hints, language. |
| 2 | Triage  | `relay-triage-extraction-agent`    | Classifies category, urgency (P1–P4 + SLA), sentiment, spam risk, risk flags. |
| 3 | Intent  | `relay-intent-router-agent`        | Decides the customer's intent + confidence; **routes uncertain cases to a human-review queue**. |
| 4 | Task    | `relay-task-execution-agent`       | Uses **mock D365 MCP** tools to retrieve account/contact/opportunity/service context, then plans + executes a downstream operation (open case, raise credit memo, schedule callback, flag churn). |
| 5 | Outcome | `relay-outcome-reporter-agent`     | Produces final status, SLA result, a drafted customer reply, next actions, and a full **audit trail**. |

Every case carries an **agent timeline** (per-stage engine, decision, confidence, timing) so the flow
is visibly traceable — enterprise, not toy.

## Run it locally

```pwsh
cd apps/web
dotnet run
# browse http://localhost:5xxx  (the console prints the port)
```

The app boots with the **offline engine** (Foundry disabled). Click **Run whole mailbox** to process
the seeded inbox, or paste your own email on the Email page. Uncertain cases appear under
**Human queue** with an inline "confirm intent" control.

### One-command smoke

```pwsh
pwsh ./scripts/smoke.ps1     # builds, launches, asserts the full pipeline, tears down
```

### Tests

```pwsh
dotnet test                  # xunit, WebApplicationFactory end-to-end over the offline pipeline
```

## Enabling the live Foundry path

Set these (env or `appsettings`), then the app uses Foundry and falls back to offline per-stage on any
error:

```
Foundry__Enabled=true
Foundry__ProjectEndpoint=https://<your-foundry-project-endpoint>
Foundry__ModelDeployment=gpt-4o
```

Auth is keyless (`DefaultAzureCredential` / managed identity). Health surfaces at `/api/health` and the
live probe at `/api/health/foundry` (`foundryMode` = `live` | `offline`).

## Mock D365 MCP

`Services/Mcp/MockD365McpServer.cs` stands in for a real Model Context Protocol server fronting
Dataverse/D365. It exposes a named tool catalog (`customer.search`, `account.get`, `contact.get`,
`opportunity.list`, `service.cases.list`, `case.create`, `creditmemo.raise`, `callback.create`,
`churn.flag`) over an in-memory dataset seeded from `Data/seed-d365.json`. Every call returns a recorded
`McpToolCall` (args + JSON result + timing) rendered as expandable cards on the Task page.

## Infrastructure (`bicep/`)

`main.bicep` + modules provision: Log Analytics / App Insights, Storage (managed-identity only, case +
audit journal), Key Vault, **Microsoft Foundry** (AI Services account + project + `gpt-4o` deployment),
Standard App Service (Linux, .NET 10), and least-privilege managed-identity RBAC.

```pwsh
az deployment group create -g <rg> -f bicep/main.bicep -p bicep/main.bicepparam
```

## CI/CD (`.github/workflows/`)

- `proj45_email_triage_intent_task_infra.yml` — what-if + deploy the bicep (manual dispatch).
- `proj45_email_triage_intent_task_deploy.yml` — build, publish, zip-deploy to App Service, assert
  health + live Foundry.

## Layout

```
apps/web/        .NET 10 Razor Pages SPA + minimal API
  Models/        domain records + options
  Services/      offline pipeline, mailbox watch, case store, human queue, agent instructions
    Foundry/     live Foundry pipeline + lenient JSON
    Mcp/         mock D365 MCP server
  Data/          seed-emails.json, seed-d365.json
  Pages/         Index (SPA shell) + Error
  wwwroot/       site.css, app.js (5 hash-routed views)
bicep/           main + modules + param
tests/           xunit end-to-end API tests
scripts/         smoke.ps1
docs/            solution.md, task.md, todo.md
```
