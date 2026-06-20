# Project: Fabrikam Internal Knowledge Assistant

## Statement of Work (Technical Brief)

### Business context
Fabrikam wants a small internal "ask the handbook" assistant for its 400 head-office staff. Employees
should be able to ask questions about HR policy, IT procedures, and travel rules and get a grounded answer
with a citation to the source policy document. This is an internal productivity pilot with a low budget.

### Functional requirements
- A simple internal web front end (single sign-on assumed) for asking questions.
- Generative AI assistant using file search / retrieval-augmented generation over a small set of policy PDFs
  and wiki exports.
- A GPT-class prompt agent that answers in plain language and cites the source section.
- No relational database required; conversation history can be kept in lightweight blob storage.

### Non-functional requirements
- Expected scale: ~400 staff, a few thousand questions per month, very low peak load.
- Data sensitivity: internal-only business documents; minimal PII.
- Observability: basic usage metrics and error logging.
- Security: managed identity, secrets vault, HTTPS only.

### Constraints & assumptions
- Primary Azure region is Australia East.
- This is a low-cost internal pilot; favour the smallest viable SKUs and consumption pricing.
- Not a mission-critical production system — single region, no HA required.

### Out of scope (for this phase)
- Public/customer access.
- Integration with the HR system of record.
- Multi-language support.
