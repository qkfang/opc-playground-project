# LEGO Website (Project: 20260606_lego_site)

## Goal
Build a LEGO website ("C") — interpreted as a simple **marketplace-style** site where users can browse LEGO sets, view details, and create/manage listings.

## Assumptions (from Dan: "c, yes")
- "C" = Marketplace.
- "yes" = proceed with recommended stack.

## Target Architecture (recommended)
- **Frontend:** Next.js (React + TypeScript)
- **Hosting:** Azure Static Web Apps (SWA)
- **Backend API:** Azure Functions (TypeScript) integrated as SWA API
- **Database:** Azure Cosmos DB (NoSQL)
- **Auth:** SWA built-in authentication (GitHub) + roles (optional)

## MVP Features
### Public
- Home page + featured sets
- Browse sets (seed data) + search/filter
- Set detail page
- Browse marketplace listings
- Listing detail page

### Authenticated
- Sign-in
- Create listing (set, condition, price, notes, photos URL)
- My listings (edit/delete)

## Data Model (initial)
- `Set`:
  - id, name, theme, year, pieces, imageUrl
- `Listing`:
  - id, setId, title, condition, price, currency, description, sellerUserId, createdAt, status

## Non-goals (MVP)
- Payments/shipping integrations
- Real photo uploads (start with imageUrl)
- Advanced moderation

## Success Criteria
- Deployed SWA site is accessible.
- Users can sign in, create a listing, and see it appear in marketplace.
