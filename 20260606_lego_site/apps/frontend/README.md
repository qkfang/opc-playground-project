# LEGO Marketplace Frontend

Next.js (TypeScript) frontend scaffold for `20260606_lego_site` Task 1.

## Local run

From this folder:

```bash
npm install
npm run dev
```

Open http://localhost:3000.

## Available routes

- `/` Home
- `/sets` Browse Sets
- `/sets/[id]` Set Detail
- `/marketplace` Marketplace
- `/marketplace/[id]` Listing Detail
- `/my-listings` My Listings
- `/my-listings/new` Create Listing
- `/my-listings/[id]/edit` Edit Listing

## Verification

```bash
npm run lint
npm run build
```

The UI uses API calls to `/api/*`. Route handlers under `app/api` provide mock-backed responses until backend work is completed.
