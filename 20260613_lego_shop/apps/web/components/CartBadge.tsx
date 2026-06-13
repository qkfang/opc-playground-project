"use client";

import { useEffect, useState } from "react";

export const CART_UPDATED_EVENT = "lego-cart-updated";

export function emitCartUpdated(itemCount?: number) {
  if (typeof window === "undefined") return;
  window.dispatchEvent(
    new CustomEvent(CART_UPDATED_EVENT, { detail: { itemCount } }),
  );
}

export default function CartBadge() {
  const [count, setCount] = useState<number | null>(null);

  async function refresh() {
    try {
      const res = await fetch("/api/cart", { cache: "no-store" });
      if (!res.ok) return;
      const data = await res.json();
      setCount(data.cart.itemCount ?? 0);
    } catch {
      /* ignore */
    }
  }

  useEffect(() => {
    refresh();
    function onUpdate(e: Event) {
      const detail = (e as CustomEvent).detail as
        | { itemCount?: number }
        | undefined;
      if (typeof detail?.itemCount === "number") {
        setCount(detail.itemCount);
      } else {
        refresh();
      }
    }
    window.addEventListener(CART_UPDATED_EVENT, onUpdate);
    return () => window.removeEventListener(CART_UPDATED_EVENT, onUpdate);
  }, []);

  if (!count) {
    return <span>Cart</span>;
  }

  return (
    <span className="inline-flex items-center gap-2">
      Cart
      <span className="inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-amber-500 px-1.5 text-xs font-bold text-white">
        {count}
      </span>
    </span>
  );
}
