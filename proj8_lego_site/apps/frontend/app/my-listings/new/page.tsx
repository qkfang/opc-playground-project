"use client";

import { useEffect, useState } from "react";
import ListingForm from "@/app/components/listing-form";
import { fetchSets } from "@/lib/api";
import { fetchCurrentUser, type CurrentUser } from "@/lib/auth";
import type { LegoSet } from "@/lib/types";

export default function CreateListingPage() {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [isAuthLoading, setIsAuthLoading] = useState(true);
  const [isLoading, setIsLoading] = useState(false);
  const [sets, setSets] = useState<LegoSet[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchCurrentUser()
      .then((currentUser) => {
        setUser(currentUser);
        if (!currentUser) {
          setSets([]);
          return;
        }

        setIsLoading(true);
        return fetchSets()
          .then(setSets)
          .catch((loadError) => {
            setError(loadError instanceof Error ? loadError.message : "Failed to load sets.");
          })
          .finally(() => setIsLoading(false));
      })
      .finally(() => setIsAuthLoading(false));
  }, []);

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Create Listing</h1>
      {error && <p className="text-red-600">{error}</p>}
      {isAuthLoading && <p className="text-slate-600">Checking sign-in status...</p>}
      {!isAuthLoading && !user && (
        <p>
          Sign in to create a listing.{" "}
          <a className="text-blue-700" href="/.auth/login/github">
            Sign in
          </a>
        </p>
      )}
      {user && isLoading && <p className="text-slate-600">Loading sets...</p>}
      {user && !isLoading && sets.length === 0 && !error && (
        <p className="text-slate-600">No sets available right now.</p>
      )}
      {user && sets.length > 0 && <ListingForm sets={sets} />}
    </section>
  );
}
