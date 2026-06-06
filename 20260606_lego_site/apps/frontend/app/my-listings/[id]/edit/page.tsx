"use client";

import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import ListingForm from "@/app/components/listing-form";
import { fetchListing, fetchSets } from "@/lib/api";
import { fetchCurrentUser, type CurrentUser } from "@/lib/auth";
import type { LegoSet, Listing } from "@/lib/types";

export default function EditListingPage() {
  const params = useParams<{ id: string }>();
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [isAuthLoading, setIsAuthLoading] = useState(true);
  const [isLoading, setIsLoading] = useState(false);
  const [sets, setSets] = useState<LegoSet[]>([]);
  const [listing, setListing] = useState<Listing | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) {
      return;
    }

    fetchCurrentUser()
      .then((currentUser) => {
        setUser(currentUser);
        if (!currentUser) {
          return;
        }

        setIsLoading(true);
        return Promise.all([fetchSets(), fetchListing(params.id)])
          .then(([loadedSets, currentListing]) => {
            setSets(loadedSets);
            setListing(currentListing);
          })
          .catch((loadError) => {
            setError(loadError instanceof Error ? loadError.message : "Failed to load listing.");
          })
          .finally(() => setIsLoading(false));
      })
      .finally(() => setIsAuthLoading(false));
  }, [params.id]);

  const canEdit = user && listing && listing.sellerUserId === user.userId;

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Edit Listing</h1>
      {error && <p className="text-red-600">{error}</p>}
      {isAuthLoading && <p className="text-slate-600">Checking sign-in status...</p>}
      {!isAuthLoading && !user && (
        <p>
          Sign in to edit listings.{" "}
          <a className="text-blue-700" href="/.auth/login/github">
            Sign in
          </a>
        </p>
      )}
      {user && isLoading && <p className="text-slate-600">Loading listing...</p>}
      {user && listing && !canEdit && (
        <p className="text-red-600">You can only edit your own listings.</p>
      )}
      {canEdit && sets.length > 0 && <ListingForm sets={sets} listing={listing} />}
    </section>
  );
}
