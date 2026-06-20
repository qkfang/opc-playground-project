# Project: Adventure Works Customer Portal Modernization

## Statement of Work (Technical Brief)

### Business context
Adventure Works runs an ageing on-premises customer portal where B2B customers place orders, track
shipments, and download invoices. The business wants to modernise it as a cloud-hosted web application
with an API back end and an AI assistant that answers customer questions about their orders and the
product catalogue, grounded in product documentation and order data.

### Functional requirements
- A customer-facing web front end (responsive) for orders, shipment tracking, and invoice downloads.
- An HTTP API (REST/OpenAPI) for the portal and for partner integrations.
- Generative AI assistant with file search / retrieval-augmented generation over product manuals,
  FAQs, and policy documents.
- A GPT-class prompt agent that answers customer questions and can summarise an order's status.
- A relational database for orders, shipments, and invoices.
- Serverless background jobs for invoice generation and shipment-status webhooks.

### Non-functional requirements
- Expected scale: approximately 40,000 monthly active business users, steady peak load of low-hundreds of
  requests per second. Production, business-important system with good availability.
- Data sensitivity: contains customer PII and commercial/financial data; encryption and least-privilege
  access required.
- Observability: end-to-end tracing, metrics, and centralized logging.
- Security: managed identity, secrets vault, HTTPS only, private storage.

### Constraints & assumptions
- Primary Azure region is Australia East.
- Favour managed PaaS; size for steady growth over the next two years.
- Single region for this phase; design so HA can be added later.

### Out of scope (for this phase)
- Payment processing (handled by an existing gateway).
- Native mobile apps.
- Data-warehouse / BI reporting (separate initiative).
