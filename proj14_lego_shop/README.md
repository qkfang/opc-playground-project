# Brick Bazaar — Lego Shop (proj14)

A small MVP Lego storefront with an HTTP API backend, built as a single Next.js
(App Router) app. The frontend is wired to real API route handlers under `app/api`.

## Stack
- Next.js 16 (App Router) + React 19 + Tailwind v4
- API: Next.js Route Handlers (Node runtime)
- Data: in-memory seed catalog + cookie-keyed in-memory cart (MVP, no DB)

## Run locally
```bash
cd apps/web
npm install
npm run dev      # http://localhost:3000
```

## Build
```bash
cd apps/web
npm run build
npm run start    # serves the production build on :3000
```

## API smoke test
Start the app (dev or start), then in another shell:
```bash
cd apps/web
npm run smoke    # defaults to http://localhost:3000
# or: node scripts/smoke.mjs http://localhost:3001
```

## Pages
- `/` — home (hero, featured sets, shop-by-theme)
- `/shop` — full catalog with theme filter (`/shop?theme=City`)
- `/shop/[id]` — product detail
- `/cart` — cart with qty controls, remove, clear, checkout placeholder

## API
- `GET /api/health`
- `GET /api/products` (`?theme=`, `?featured=true`)
- `GET /api/products/:id`
- `GET|POST|PATCH|DELETE /api/cart`

See `docs/design.md` for the full contract and `docs/tasks.md` for the breakdown.
