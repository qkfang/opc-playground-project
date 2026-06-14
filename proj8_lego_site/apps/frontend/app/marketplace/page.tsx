"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { fetchListings } from "@/lib/api";
import type { Listing } from "@/lib/types";

export default function MarketplacePage() {
  const [listings, setListings] = useState<Listing[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchListings()
      .then(setListings)
      .catch((loadError) => {
        setError(loadError instanceof Error ? loadError.message : "Failed to load listings.");
      });
  }, []);

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Marketplace</h1>
      {error && <p className="text-red-600">{error}</p>}
      {!error && listings === null && <p className="text-slate-600">Loading listings...</p>}
      {!error && listings?.length === 0 && (
        <p className="text-slate-600">No listings available yet.</p>
      )}
      <ul className="space-y-3">
        {listings?.map((listing) => (
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
