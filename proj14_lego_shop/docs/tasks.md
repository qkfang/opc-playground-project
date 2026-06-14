# Lego Shop Website + API — Tasks (proj14)

task_id: build-lego-shop-website-api-20260613-1419

Status legend: TODO / DOING / DONE

## Infra / Scaffold
- [DONE] Create project folder `20260613_lego_shop` with `docs/` + `apps/web`
- [DONE] Write `docs/design.md`
- [DONE] Write `docs/tasks.md`
- [DONE] Scaffold Next.js app config (package.json, tsconfig, next.config, tailwind, globals)

## Backend (API)
- [DONE] `lib/types.ts` — shared types
- [DONE] `lib/catalog.ts` — seed Lego product data + query helpers
- [DONE] `lib/cart-store.ts` — in-memory cart store + cart-id cookie helpers + CartView builder
- [DONE] `app/api/health/route.ts` — health endpoint
- [DONE] `app/api/products/route.ts` — list + filter (theme, featured)
- [DONE] `app/api/products/[id]/route.ts` — product detail
- [DONE] `app/api/cart/route.ts` — GET/POST/PATCH/DELETE cart

## Frontend (Storefront)
- [DONE] `app/layout.tsx` — shell + nav + cart badge
- [DONE] `app/globals.css` — base styles
- [DONE] `app/page.tsx` — home (hero + featured + themes)
- [DONE] `app/shop/page.tsx` — catalog with theme filter
- [DONE] `app/shop/[id]/page.tsx` — product detail
- [DONE] `app/cart/page.tsx` — cart view
- [DONE] `components/AddToCartButton.tsx` — client add-to-cart
- [DONE] `components/CartControls.tsx` — client qty/remove controls
- [DONE] `components/CartBadge.tsx` — client cart count badge

## Verification
- [DONE] `npm install`
- [DONE] `npm run build` passes
- [DONE] `scripts/smoke.mjs` API round-trip passes
- [DONE] Browser checks for `/`, `/shop`, `/shop/[id]`, `/cart`

## Handoff
- [DONE] Update `shared-context/projects/proj14.md` with notes + evidence
- [DONE] Post DONE in Build topic
- [DONE] Hand off to toadette with strict envelope
