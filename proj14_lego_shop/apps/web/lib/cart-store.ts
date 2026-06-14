import { cookies } from "next/headers";
import { randomUUID } from "node:crypto";
import type { Cart, CartView } from "./types";
import { getProduct } from "./catalog";

export const CART_COOKIE = "lego_cart_id";

// In-memory cart store. Resets on server restart — acceptable for MVP.
const store = new Map<string, Cart>();

function getOrCreateCart(id: string): Cart {
  let cart = store.get(id);
  if (!cart) {
    cart = { id, items: [] };
    store.set(id, cart);
  }
  return cart;
}

/**
 * Resolve the current cart id from the cookie, creating + setting one if absent.
 * Must be called within a request (route handler / server component) context.
 */
export async function resolveCartId(): Promise<string> {
  const jar = await cookies();
  let id = jar.get(CART_COOKIE)?.value;
  if (!id) {
    id = randomUUID();
    jar.set(CART_COOKIE, id, {
      httpOnly: true,
      sameSite: "lax",
      path: "/",
      maxAge: 60 * 60 * 24 * 30,
    });
  }
  return id;
}

export function buildCartView(cart: Cart): CartView {
  const lines = cart.items
    .map((item) => {
      const product = getProduct(item.productId);
      if (!product) return null;
      return {
        product,
        quantity: item.quantity,
        lineTotal: Math.round(product.price * item.quantity * 100) / 100,
      };
    })
    .filter((l): l is NonNullable<typeof l> => l !== null);

  const itemCount = lines.reduce((sum, l) => sum + l.quantity, 0);
  const subtotal =
    Math.round(lines.reduce((sum, l) => sum + l.lineTotal, 0) * 100) / 100;

  return { id: cart.id, lines, itemCount, subtotal };
}

export function getCartView(id: string): CartView {
  return buildCartView(getOrCreateCart(id));
}

export function addToCart(id: string, productId: string, quantity = 1): CartView | null {
  if (!getProduct(productId)) return null;
  const qty = Math.max(1, Math.floor(quantity));
  const cart = getOrCreateCart(id);
  const existing = cart.items.find((i) => i.productId === productId);
  if (existing) {
    existing.quantity += qty;
  } else {
    cart.items.push({ productId, quantity: qty });
  }
  return buildCartView(cart);
}

export function setQuantity(id: string, productId: string, quantity: number): CartView {
  const cart = getOrCreateCart(id);
  const qty = Math.floor(quantity);
  const existing = cart.items.find((i) => i.productId === productId);
  if (qty <= 0) {
    cart.items = cart.items.filter((i) => i.productId !== productId);
  } else if (existing) {
    existing.quantity = qty;
  } else if (getProduct(productId)) {
    cart.items.push({ productId, quantity: qty });
  }
  return buildCartView(cart);
}

export function removeFromCart(id: string, productId: string): CartView {
  const cart = getOrCreateCart(id);
  cart.items = cart.items.filter((i) => i.productId !== productId);
  return buildCartView(cart);
}

export function clearCart(id: string): CartView {
  const cart = getOrCreateCart(id);
  cart.items = [];
  return buildCartView(cart);
}
