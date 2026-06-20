# Project: Wingtip Retail Analytics API

## Statement of Work (Technical Brief)

### Business context
Wingtip Traders wants a back-end analytics API that ingests point-of-sale events from 1,200 stores
and exposes near-real-time sales and inventory metrics to internal dashboards. There is no customer-facing
UI in this phase — the deliverable is a robust, well-instrumented HTTP API and the data plumbing behind it.

### Functional requirements
- A documented HTTP API (REST/OpenAPI) returning aggregated sales, basket, and inventory metrics.
- High-volume event ingestion: serverless functions process point-of-sale events and write to storage.
- A relational database for curated, queryable aggregates that power the dashboards.
- Blob storage for raw event archives and exported reports.
- This is a classic data and integration workload focused on throughput, not analytics narratives.

### Non-functional requirements
- Expected scale: ~1,200 stores emitting events, tens of millions of function executions per month,
  sustained moderate API traffic from internal dashboards.
- Data sensitivity: internal commercial data; no PII beyond store/operator identifiers.
- Observability: detailed metrics, tracing, and ingestion lag monitoring are essential.
- Security: managed identity, secrets vault, HTTPS only, private storage.

### Constraints & assumptions
- Primary Azure region is Australia East.
- Optimise for throughput and cost-efficiency at high event volume (consumption-based compute preferred).
- Production system feeding finance and operations dashboards.

### Out of scope (for this phase)
- Any web/UI front end (API only).
- Predictive forecasting (a later phase).
- Cross-region replication.
