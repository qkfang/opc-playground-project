"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { deleteListing, fetchListing } from "@/lib/api";
import { fetchCurrentUser, type CurrentUser } from "@/lib/auth";
import type { Listing } from "@/lib/types";

export default function ListingDetailPage() {
  const params = useParams<{ id: string }>();
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [listing, setListing] = useState<Listing | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) {
      return;
    }

    Promise.all([fetchListing(params.id), fetchCurrentUser()])
      .then(([loadedListing, currentUser]) => {
        setListing(loadedListing);
        setUser(currentUser);
      })
      .catch((loadError) => {
        setError(loadError instanceof Error ? loadError.message : "Listing not found.");
        setListing(null);
      });
  }, [params.id]);

  async function onDelete() {
    if (!listing) {
      return;
    }

    setError(null);
    try {
      await deleteListing(listing.id);
      setListing(null);
    } catch (deleteError) {
      setError(deleteError instanceof Error ? deleteError.message : "Failed to delete listing.");
    }
  }

  const isOwner = Boolean(user && listing && user.userId === listing.sellerUserId);

  return (
    <section className="space-y-4">
      <Link className="text-blue-700" href="/marketplace">
        ← Back to Marketplace
      </Link>
      {error && <p className="text-red-600">{error}</p>}
      {listing === undefined && !error && <p className="text-slate-600">Loading listing...</p>}
      {listing === null && !error && (
        <p className="text-slate-600">This listing is no longer available.</p>
      )}
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
          {isOwner && (
            <div className="mt-4 flex gap-4">
              <Link className="text-blue-700" href={`/my-listings/${listing.id}/edit`}>
                Edit listing
              </Link>
              <button type="button" className="text-red-700" onClick={onDelete}>
                Delete listing
              </button>
            </div>
          )}
        </article>
      )}
    </section>
  );
}
