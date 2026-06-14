# Lego Shop Website + API — Design (proj14)

task_id: build-lego-shop-website-api-20260613-1419
project_id: proj14
location: `repos/opc-playground-project/20260613_lego_shop`

## 1. Goal & MVP Scope

Build a small but real Lego shop storefront wired to an HTTP API backend, runnable
and verifiable locally with a single toolchain.

In scope (MVP):
- Product catalog listing (browse all Lego sets)
- Featured/home section (hero + featured products)
- Product detail view (per-set page)
- Category filtering on the catalog
- A simple cart (add / view / update qty / remove) backed by API + client state
- Backend HTTP API serving product + cart data
- Local verification (build + smoke tests + browser checks)

Out of scope (explicitly deferred for MVP):
- Real payment / checkout processing (not essential for MVP)
- User accounts / auth
- Persistent database (in-memory + seed data is sufficient for MVP)
- Admin product management
- Cloud deployment (initial request is "create the website with API backend first")

## 2. Architecture

Single Next.js (App Router) application under `apps/web`:
- **Frontend**: React Server Components + a few client components for cart interactivity.
- **Backend API**: Next.js Route Handlers under `app/api/**`. These are real server
  endpoints (Node runtime) returning JSON. The frontend fetches from these endpoints
  for core shop flows.
- **Data layer**: A shared in-memory product catalog (`lib/catalog.ts`) seeded with
  Lego sets, plus an in-memory cart store (`lib/cart-store.ts`) keyed by a cart id
  cookie. This keeps the MVP self-contained — no external DB required — while still
  exercising a genuine client → API → data path.

Why a single app instead of separate frontend+backend processes:
- One `npm install` / `npm run build` / `npm run dev` toolchain.
- QA can verify everything locally without standing up Azure Functions, Cosmos, or a
  second server process.
- The API is still a distinct, independently-callable HTTP surface (`/api/...`).

```
Browser ──▶ Next.js pages (RSC) ──▶ fetch(/api/...) ──▶ Route Handlers ──▶ in-memory data
                                   ◀── JSON ──────────────────────────────┘
```

## 3. Data Model

Product (Lego set):
```ts
type Product = {
  id: string;            // slug, e.g. "classic-creator-castle"
  name: string;
  theme: string;        // category, e.g. "City", "Technic", "Creator"
  price: number;        // AUD
  pieces: number;
  ageRange: string;     // e.g. "8+"
  rating: number;        // 0..5
  featured: boolean;
  blurb: string;        // short marketing line
  description: string;   // longer detail text
  colorFrom: string;     // tailwind gradient start (visual only)
  colorTo: string;       // tailwind gradient end (visual only)
};
```

Cart:
```ts
type CartItem = { productId: string; quantity: number };
type Cart = { id: string; items: CartItem[] };
type CartView = {
  id: string;
  lines: Array<{ product: Product; quantity: number; lineTotal: number }>;
  itemCount: number;
  subtotal: number;
};
```

## 4. API Contract

Base path: `/api`

- `GET /api/products`
  - Query: `?theme=<theme>` (optional), `?featured=true` (optional)
  - 200 → `{ products: Product[], themes: string[] }`
- `GET /api/products/:id`
  - 200 → `{ product: Product }`
  - 404 → `{ error: "not_found" }`
- `GET /api/cart`
  - Reads cart-id cookie (creates one if missing)
  - 200 → `{ cart: CartView }`
- `POST /api/cart`
  - Body: `{ productId: string, quantity?: number }` (quantity defaults 1, adds to existing)
  - 200 → `{ cart: CartView }`
  - 400 → `{ error: "invalid_product" }`
- `PATCH /api/cart`
  - Body: `{ productId: string, quantity: number }` (sets absolute qty; 0 removes)
  - 200 → `{ cart: CartView }`
- `DELETE /api/cart`
  - Body: `{ productId: string }` (removes one line) OR `{ clear: true }` (empties cart)
  - 200 → `{ cart: CartView }`
- `GET /api/health`
  - 200 → `{ status: "ok", products: <count> }`

All responses are JSON. Cart identity is a signed-free opaque uuid stored in an
`httpOnly` cookie (`lego_cart_id`).

## 5. UI / Pages

- `/` — Home: hero, featured sets grid, theme highlights, link to full catalog.
- `/shop` — Catalog: all sets, theme filter chips, add-to-cart buttons.
- `/shop/[id]` — Product detail: large hero, specs (pieces/age/rating/theme), add-to-cart.
- `/cart` — Cart: line items, qty controls, remove, subtotal, "checkout" placeholder note.
- Shared header nav: Home, Shop, Cart (with item count badge).

Visual style follows the existing site's clean Tailwind aesthetic (gradient hero,
rounded cards, slate text).

## 6. Verification Plan

- `npm install` then `npm run build` passes (production build).
- API smoke script (`scripts/smoke.mjs`) starts a built server and asserts:
  - `/api/health` → ok
  - `/api/products` → non-empty list + themes
  - `/api/products/:id` → known set
  - cart POST/GET/PATCH/DELETE round-trip works
- Browser checks: `/`, `/shop`, `/shop/:id`, `/cart` render; add-to-cart updates badge.

## 7. Assumptions

- AUD pricing, static seed catalog (~9–12 sets across several themes).
- Cart is per-browser (cookie), resets on server restart — acceptable for MVP.
- No payment; checkout button shows an MVP placeholder message.
