# Tasks — 20260606_lego_site

## Task 1 — Frontend scaffold (Copilot)
**Goal:** Create a Next.js (TS) frontend with marketplace pages.
- Pages: Home, Browse Sets, Set Detail, Marketplace, Listing Detail, My Listings, Create/Edit Listing
- Styling: Tailwind (ok) + simple components
- Data: call API endpoints (mock until backend ready)
- Verification: `npm test` (if any) + `npm run lint` + `npm run build`

## Task 2 — Backend API + DB (Copilot)
**Goal:** Azure Functions (TypeScript) API for sets + listings with Cosmos DB.
- Endpoints:
  - GET /api/sets
  - GET /api/sets/{id}
  - GET /api/listings
  - GET /api/listings/{id}
  - POST /api/listings (auth)
  - PUT /api/listings/{id} (auth, owner)
  - DELETE /api/listings/{id} (auth, owner)
- Data access: Cosmos SDK
- Verification: local functions start + basic API calls

## Task 3 — Integration + auth gating (Copilot)
**Goal:** Frontend uses real API, auth gates listing CRUD, and end-to-end flow works.
- Sign-in UX (SWA auth)
- Owner checks
- Friendly error handling
- Verification: local run + create listing flow
