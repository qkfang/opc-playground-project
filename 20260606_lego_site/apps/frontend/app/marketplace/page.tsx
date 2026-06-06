"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { fetchListings } from "@/lib/api";
import type { Listing } from "@/lib/types";

export default function MarketplacePage() {
  const [listings, setListings] = useState<Listing[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchListings()
      .then(setListings)
      .catch(() => setError("Failed to load listings."));
  }, []);

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Marketplace</h1>
      {error && <p className="text-red-600">{error}</p>}
      <ul className="space-y-3">
        {listings.map((listing) => (
          <li key={listing.id} className="rounded border bg-white p-4">
            <h2 className="text-lg font-medium">{listing.title}</h2>
            <p className="text-sm text-slate-600">
              Condition: {listing.condition} • {listing.currency} {listing.price.toFixed(2)}
            </p>
            <Link
              className="mt-2 inline-block text-blue-700"
              href={`/marketplace/${listing.id}`}
            >
              View listing detail
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
