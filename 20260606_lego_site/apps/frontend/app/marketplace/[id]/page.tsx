"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { fetchListing } from "@/lib/api";
import type { Listing } from "@/lib/types";

export default function ListingDetailPage() {
  const params = useParams<{ id: string }>();
  const [listing, setListing] = useState<Listing | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) {
      return;
    }

    fetchListing(params.id)
      .then(setListing)
      .catch(() => setError("Listing not found."));
  }, [params.id]);

  return (
    <section className="space-y-4">
      <Link className="text-blue-700" href="/marketplace">
        ← Back to Marketplace
      </Link>
      {error && <p className="text-red-600">{error}</p>}
      {listing && (
        <article className="rounded border bg-white p-4">
          <h1 className="text-2xl font-semibold">{listing.title}</h1>
          <p>Set ID: {listing.setId}</p>
          <p>Condition: {listing.condition}</p>
          <p>
            Price: {listing.currency} {listing.price.toFixed(2)}
          </p>
          <p>Seller: {listing.sellerUserId}</p>
          <p className="text-slate-700">{listing.description}</p>
        </article>
      )}
    </section>
  );
}
