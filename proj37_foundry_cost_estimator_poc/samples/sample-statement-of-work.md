# Project: Contoso Claims Intelligence Platform

## Statement of Work (Technical Brief)

### Business context
Contoso Insurance receives thousands of claims documents (PDFs, scanned forms, emails) every day.
The business wants an enterprise web application that lets claims handlers upload documents and get an
AI-generated summary, extracted entities, and a recommended next action. The goal is to reduce manual
triage time and improve consistency across the claims team.

### Functional requirements
- A secure web portal (front end) where claims handlers sign in and upload one or more documents.
- An HTTP API (REST/OpenAPI) so the existing policy-administration system can submit claims programmatically.
- Generative AI document understanding: ingest the uploaded documents, build a knowledge base, and answer
  handler questions grounded in the documents (retrieval-augmented generation / file search).
- Use a large language model (GPT-class) prompt agent to extract scope, classify the claim, and draft a
  recommendation. Advanced reasoning is required for complex, multi-document claims.
- Persist structured claim records and audit history in a relational database.
- Background processing for OCR and notification webhooks should scale independently of the web tier.

### Non-functional requirements
- Expected scale: approximately 120,000 monthly active users across the enterprise, with peak load of
  several hundred requests per second during business hours. This is a mission-critical, production system
  that requires high availability.
- Data sensitivity: claims documents contain personally identifiable information (PII) and financial data.
  The platform must comply with the company's regulatory and SOC 2 obligations, including encryption,
  least-privilege access, and full auditability.
- Observability: end-to-end tracing, metrics, and centralized logging are mandatory.
- Security: no secrets in source or config; use managed identity and a secrets vault.

### Constraints & assumptions
- Primary Azure region is Australia East.
- The solution should favour managed PaaS services to minimise operations overhead.
- This is a production deployment (not a throwaway prototype), so size for resilience and growth.

### Out of scope (for this phase)
- Training a custom claims model from scratch (use pre-built / Foundry models).
- Mobile native apps.
- Migration of historical claims older than 7 years.
