# legopuzzle backend

Node.js + Express backend API for leaderboard scores.

## Endpoints

- `GET /api/health`
- `GET /api/leaderboard?limit=10`
- `POST /api/scores`

## Environment variables

- `PORT` (optional, default `3000`)
- `COSMOS_ENDPOINT`
- `COSMOS_KEY`
- `COSMOS_DATABASE_ID`
- `COSMOS_CONTAINER_ID`

Set all Cosmos variables to persist scores in Azure Cosmos DB. If unset, the app uses an in-memory store (useful for local testing only).

## Run

```bash
npm install
npm test
npm start
```
