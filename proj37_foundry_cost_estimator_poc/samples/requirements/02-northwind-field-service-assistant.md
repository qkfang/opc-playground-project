# Project: Northwind Field Service Assistant

## Statement of Work (Technical Brief)

### Business context
Northwind Logistics runs a national fleet of field technicians who service refrigeration and HVAC
equipment. Technicians currently phone a help desk to look up manuals, warranty terms, and repair
procedures. The business wants a lightweight web + mobile-friendly assistant that answers technician
questions grounded in the equipment manuals and past work orders, so first-time-fix rates improve.

### Functional requirements
- A responsive web front end usable on tablets in the field.
- An HTTP API so the dispatch system can pre-load the relevant equipment context for a job.
- Generative AI assistant with file search / retrieval-augmented generation over a corpus of equipment
  manuals, service bulletins, and historical work orders.
- A GPT-class prompt agent that drafts step-by-step repair guidance and a parts list.
- Store work-order summaries and feedback in a NoSQL document store for flexible, high-write throughput.

### Non-functional requirements
- Expected scale: around 5,000 technicians, roughly 30,000 assistant queries per month, modest peak load
  (tens of requests per second).
- Data sensitivity: internal operational data; limited PII (technician identifiers). No regulated financial data.
- Observability: request tracing and basic usage metrics.
- Security: managed identity and a secrets vault; HTTPS only.

### Constraints & assumptions
- Primary Azure region is Australia East.
- Favour managed PaaS and consumption-based services to keep run cost low.
- This is a production pilot for one business unit, with room to grow.

### Out of scope (for this phase)
- Offline mode / on-device inference.
- Integration with third-party parts-ordering systems.
- Native iOS/Android apps (responsive web only for now).
