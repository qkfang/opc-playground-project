"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { emitCartUpdated } from "./CartBadge";
import type { CartView } from "@/lib/types";

function formatPrice(value: number): string {
  return new Intl.NumberFormat("en-AU", {
    style: "currency",
    currency: "AUD",
  }).format(value);
}

export default function CartControls() {
  const [cart, setCart] = useState<CartView | null>(null);
  const [busy, setBusy] = useState(false);
  const [checkoutNote, setCheckoutNote] = useState(false);

  function apply(next: CartView) {
    setCart(next);
    emitCartUpdated(next.itemCount);
  }

  async function load() {
    const res = await fetch("/api/cart", { cache: "no-store" });
    if (res.ok) setCart((await res.json()).cart);
  }

  useEffect(() => {
    load();
  }, []);

  async function setQty(productId: string, quantity: number) {
    setBusy(true);
    try {
      const res = await fetch("/api/cart", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ productId, quantity }),
      });
      if (res.ok) apply((await res.json()).cart);
    } finally {
      setBusy(false);
    }
  }

  async function remove(productId: string) {
    setBusy(true);
    try {
      const res = await fetch("/api/cart", {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ productId }),
      });
      if (res.ok) apply((await res.json()).cart);
    } finally {
      setBusy(false);
    }
  }

  async function clear() {
    setBusy(true);
    try {
      const res = await fetch("/api/cart", {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ clear: true }),
      });
      if (res.ok) apply((await res.json()).cart);
    } finally {
      setBusy(false);
    }
  }

  if (!cart) {
    return <p className="text-slate-600">Loading your cart…</p>;
  }

  if (cart.lines.length === 0) {
    return (
      <div className="rounded-3xl border border-dashed border-slate-300 bg-white p-10 text-center">
        <p className="text-lg font-semibold text-slate-900">Your cart is empty</p>
        <p className="mt-2 text-sm text-slate-600">
          Browse the shop and add a few sets to get started.
        </p>
        <Link
          href="/shop"
          className="mt-5 inline-flex rounded-full bg-slate-900 px-5 py-2.5 text-sm font-semibold text-white hover:bg-slate-700"
        >
          Go to shop
        </Link>
      </div>
    );
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1.6fr_0.9fr]">
      <ul className="space-y-4">
        {cart.lines.map((line) => (
          <li
            key={line.product.id}
            className="flex flex-col gap-4 rounded-3xl border border-slate-200 bg-white p-5 sm:flex-row sm:items-center sm:justify-between"
          >
            <div className="flex items-center gap-4">
              <div
                className={`h-16 w-16 shrink-0 rounded-2xl bg-gradient-to-br ${line.product.colorFrom} ${line.product.colorTo}`}
              />
              <div>
                <Link
                  href={`/shop/${line.product.id}`}
                  className="font-semibold text-slate-900 hover:underline"
                >
                  {line.product.name}
                </Link>
                <p className="text-sm text-slate-500">{line.product.theme}</p>
                <p className="text-sm text-slate-700">
                  {formatPrice(line.product.price)} each
                </p>
              </div>
            </div>

            <div className="flex items-center gap-4">
              <div className="inline-flex items-center rounded-full border border-slate-300">
                <button
                  type="button"
                  aria-label="Decrease quantity"
                  disabled={busy}
                  onClick={() => setQty(line.product.id, line.quantity - 1)}
                  className="px-3 py-1.5 text-lg font-semibold text-slate-700 disabled:opacity-50"
                >
                  −
                </button>
                <span className="min-w-8 text-center text-sm font-semibold">
                  {line.quantity}
                </span>
                <button
                  type="button"
                  aria-label="Increase quantity"
                  disabled={busy}
                  onClick={() => setQty(line.product.id, line.quantity + 1)}
                  className="px-3 py-1.5 text-lg font-semibold text-slate-700 disabled:opacity-50"
                >
                  +
                </button>
              </div>
              <div className="w-20 text-right font-semibold text-slate-900">
                {formatPrice(line.lineTotal)}
              </div>
              <button
                type="button"
                disabled={busy}
                onClick={() => remove(line.product.id)}
                className="text-sm font-medium text-rose-600 hover:text-rose-800 disabled:opacity-50"
              >
                Remove
              </button>
            </div>
          </li>
        ))}
      </ul>

      <aside className="h-fit rounded-3xl border border-slate-200 bg-white p-6">
        <h2 className="text-lg font-semibold text-slate-900">Order summary</h2>
        <dl className="mt-4 space-y-2 text-sm text-slate-700">
          <div className="flex justify-between">
            <dt>Items</dt>
            <dd>{cart.itemCount}</dd>
          </div>
          <div className="flex justify-between">
            <dt>Subtotal</dt>
            <dd className="font-semibold text-slate-900">
              {formatPrice(cart.subtotal)}
            </dd>
          </div>
          <div className="flex justify-between text-slate-500">
            <dt>Shipping</dt>
            <dd>Calculated at checkout</dd>
          </div>
        </dl>
        <button
          type="button"
          onClick={() => setCheckoutNote(true)}
          className="mt-6 w-full rounded-full bg-amber-500 px-5 py-3 text-sm font-semibold text-white hover:bg-amber-600"
        >
          Checkout
        </button>
        {checkoutNote && (
          <p className="mt-3 rounded-2xl bg-amber-50 p-3 text-xs text-amber-800">
            Checkout is a placeholder in this MVP — no payment is processed.
          </p>
        )}
        <button
          type="button"
          disabled={busy}
          onClick={clear}
          className="mt-3 w-full rounded-full border border-slate-300 px-5 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50"
        >
          Clear cart
        </button>
      </aside>
    </div>
  );
}
