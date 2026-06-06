# LEGO Marketplace Backend

Azure Functions (TypeScript) backend for `20260606_lego_site` Task 2.

## Endpoints

- `GET /api/sets`
- `GET /api/sets/{id}`
- `GET /api/listings`
- `GET /api/listings/{id}`
- `POST /api/listings`
- `PUT /api/listings/{id}`
- `DELETE /api/listings/{id}`

`POST`, `PUT`, and `DELETE` require an authenticated user. In Azure Static Web Apps the backend reads the `x-ms-client-principal` header. For local-only smoke tests you can set `ALLOW_LOCAL_DEV_AUTH=true` and send `x-user-id: demo-user`.

## Local development

1. Install dependencies:

   ```bash
   npm install
   ```

2. Copy the sample settings and update values as needed:

   ```bash
   cp local.settings.sample.json local.settings.json
   ```

3. Start the function app (requires Azure Functions Core Tools v4 installed locally):

   ```bash
   npm run start
   ```

4. In another terminal, run the smoke script:

   ```bash
   npm run smoke
   ```

If `COSMOS_CONNECTION_STRING`, `COSMOS_DATABASE_NAME`, or `COSMOS_CONTAINER_NAME` are missing, the app falls back to an in-memory mock store and returns `x-data-source: mock` on responses. When Cosmos is configured, the backend uses a single container with partition key `/type` and seeds the default sets/listings if they do not exist yet.

## Run with frontend

To run end-to-end locally with the Next.js frontend:

1. Keep this backend running with `ALLOW_LOCAL_DEV_AUTH=true` in `local.settings.json`.
2. In `../frontend`, set:
   - `BACKEND_API_BASE_URL=http://localhost:7071/api`
   - `NEXT_PUBLIC_LOCAL_USER_ID=demo-user` (or another user id)
3. Start frontend with `npm run dev`.

The frontend proxies `/api/*` to this Functions app and sends `x-user-id` for local auth-gated listing CRUD.

## Verification

```bash
npm run build
npm test
```
