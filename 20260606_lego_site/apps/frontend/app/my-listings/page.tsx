"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { deleteListing, fetchListings } from "@/lib/api";
import { fetchCurrentUser, type CurrentUser } from "@/lib/auth";
import type { Listing } from "@/lib/types";

export default function MyListingsPage() {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [isAuthLoading, setIsAuthLoading] = useState(true);
  const [isLoading, setIsLoading] = useState(false);
  const [listings, setListings] = useState<Listing[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchCurrentUser()
      .then((currentUser) => {
        setUser(currentUser);
        if (!currentUser) {
          setListings([]);
          return;
        }

        setIsLoading(true);
        return fetchListings(currentUser.userId)
          .then(setListings)
          .catch((loadError) => {
            setError(loadError instanceof Error ? loadError.message : "Failed to load your listings.");
          })
          .finally(() => setIsLoading(false));
      })
      .finally(() => setIsAuthLoading(false));
  }, []);

  async function onDelete(id: string) {
    setError(null);
    try {
      await deleteListing(id);
      setListings((current) => current.filter((listing) => listing.id !== id));
    } catch (deleteError) {
      setError(deleteError instanceof Error ? deleteError.message : "Failed to delete listing.");
    }
  }

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">My Listings</h1>
        {user && (
          <Link className="rounded bg-blue-600 px-3 py-2 text-white" href="/my-listings/new">
            Create listing
          </Link>
        )}
      </div>
      {error && <p className="text-red-600">{error}</p>}
      {isAuthLoading && <p className="text-slate-600">Checking sign-in status...</p>}
      {!isAuthLoading && !user && (
        <p>
          Sign in to manage your listings.{" "}
          <a className="text-blue-700" href="/.auth/login/github">
            Sign in
          </a>
        </p>
      )}
      {user && isLoading && <p className="text-slate-600">Loading your listings...</p>}
      {user && !isLoading && listings.length === 0 && (
        <p className="text-slate-600">You do not have any listings yet.</p>
      )}
      {user && listings.length > 0 && (
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
                <button
                  type="button"
                  className="text-red-700"
                  onClick={() => onDelete(listing.id)}
                >
                  Delete
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
