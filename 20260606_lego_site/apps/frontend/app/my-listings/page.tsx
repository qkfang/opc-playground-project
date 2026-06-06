"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { fetchListings } from "@/lib/api";
import type { Listing } from "@/lib/types";

const owner = "demo-user";

export default function MyListingsPage() {
  const [listings, setListings] = useState<Listing[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchListings(owner)
      .then(setListings)
      .catch(() => setError("Failed to load your listings."));
  }, []);

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">My Listings</h1>
        <Link className="rounded bg-blue-600 px-3 py-2 text-white" href="/my-listings/new">
          Create listing
        </Link>
      </div>
      {error && <p className="text-red-600">{error}</p>}
      <ul className="space-y-3">
        {listings.map((listing) => (
          <li key={listing.id} className="rounded border bg-white p-4">
            <h2 className="text-lg font-medium">{listing.title}</h2>
            <p className="text-sm text-slate-600">
              {listing.currency} {listing.price.toFixed(2)} • {listing.status}
            </p>
            <div className="mt-2 flex gap-4">
              <Link className="text-blue-700" href={`/marketplace/${listing.id}`}>
                View
              </Link>
              <Link className="text-blue-700" href={`/my-listings/${listing.id}/edit`}>
                Edit
              </Link>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
