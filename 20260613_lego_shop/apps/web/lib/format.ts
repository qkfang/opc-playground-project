export function formatPrice(value: number): string {
  return new Intl.NumberFormat("en-AU", {
    style: "currency",
    currency: "AUD",
  }).format(value);
}

/**
 * Base URL for server-side fetches to our own API route handlers.
 * In the Node server runtime, relative fetch URLs are not allowed, so we build
 * an absolute origin from request headers at call sites; for simplicity in MVP
 * server components we read directly from the data layer instead of fetching.
 */
export function apiBase(): string {
  return process.env.NEXT_PUBLIC_BASE_URL ?? "";
}
