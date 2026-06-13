"use client";

import { useState } from "react";
import { emitCartUpdated } from "./CartBadge";

export default function AddToCartButton({
  productId,
  label = "Add to cart",
}: {
  productId: string;
  label?: string;
}) {
  const [state, setState] = useState<"idle" | "loading" | "added">("idle");

  async function add() {
    setState("loading");
    try {
      const res = await fetch("/api/cart", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ productId, quantity: 1 }),
      });
      if (!res.ok) {
        setState("idle");
        return;
      }
      const data = await res.json();
      emitCartUpdated(data.cart.itemCount);
      setState("added");
      setTimeout(() => setState("idle"), 1500);
    } catch {
      setState("idle");
    }
  }

  return (
    <button
      type="button"
      onClick={add}
      disabled={state === "loading"}
      className="inline-flex items-center justify-center rounded-full bg-slate-900 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-slate-700 disabled:opacity-60"
    >
      {state === "added" ? "Added ✓" : state === "loading" ? "Adding…" : label}
    </button>
  );
}
