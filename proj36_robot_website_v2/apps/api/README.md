# proj36 Feedback API (Cogsworth Robotics 2.0)

Azure Functions (Node.js v4 programming model) backing the **Feedback form** on the
Cogsworth Robotics 2.0 site. Deployed as **SWA managed Functions** (same Static Web App,
served under `/api/*` — no separate hosting).

## Endpoints

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/api/feedback` | Validate + save a feedback entry to the in-memory store. Returns `201` with the created entry (email masked) + running `total`, or `400` with per-field errors. |
| `GET` | `/api/feedback?limit=N` | List stored feedback, newest first (emails masked) + `count`. `limit` optional (max 200). |
| `GET` | `/api/health` | Liveness + current store size. |

### Request body (POST)
```json
{ "name": "Ada", "email": "ada@example.com", "rating": 5, "message": "Love it!" }
```
`rating` is optional (1–5, clamped). `name`, `email`, `message` are required.

## In-memory "database"
Feedback lives in a module-level array in [`src/store.js`](src/store.js) — **process-local**,
no external DB, **reset when the Functions host restarts** (by design for this demo).
Validation, id generation, rating clamping, and email masking live there and are unit-tested.

## Run locally
```bash
npm install
npm start            # func start  -> http://localhost:7071/api/...
npm test             # node --test  (store unit tests)
```
For a full front-end ↔ API local test, run this host on :7071 and serve `../web` with a
proxy that forwards `/api/*` to it (see `../temp/dev-proxy.mjs`).

## Deploy
Handled by the repo's `proj36_robot_website_v2_deploy.yml` GitHub Action, which passes
`api_location: proj36_robot_website_v2/apps/api` to `Azure/static-web-apps-deploy@v1`.
