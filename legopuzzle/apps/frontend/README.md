# legopuzzle frontend

React + Vite frontend for the Lego sliding puzzle game.

## Local run

```bash
npm install
npm run dev
```

Optional environment variable:

```bash
VITE_API_BASE_URL=http://localhost:3000
```

When `VITE_API_BASE_URL` is set, the UI calls:

- `GET /leaderboard` to load scores
- `POST /leaderboard` to submit `{ displayName, score, moves, seconds }`

If the variable is omitted, the app uses same-origin relative `/leaderboard` requests.

## Verification

```bash
npm run lint
npm run build
```
