# LEGO Marketplace Frontend

Next.js (TypeScript) frontend for `20260606_lego_site`.

## Local run

1. Start backend API in another terminal:

   ```bash
   cd ../backend
   npm install
   cp local.settings.sample.json local.settings.json
   # set ALLOW_LOCAL_DEV_AUTH=true in local.settings.json
   npm run start
   ```

2. In this folder:

```bash
npm install
# defaults to http://localhost:7071/api
export BACKEND_API_BASE_URL=http://localhost:7071/api
# optional local auth identity used for CRUD owner checks
export NEXT_PUBLIC_LOCAL_USER_ID=demo-user
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

The UI calls `/api/*` route handlers that proxy to the Azure Functions backend (`BACKEND_API_BASE_URL`).
