"use client";

import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import ListingForm from "@/app/components/listing-form";
import { fetchListing, fetchSets } from "@/lib/api";
import type { LegoSet, Listing } from "@/lib/types";

export default function EditListingPage() {
  const params = useParams<{ id: string }>();
  const [sets, setSets] = useState<LegoSet[]>([]);
  const [listing, setListing] = useState<Listing | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) {
      return;
    }

    Promise.all([fetchSets(), fetchListing(params.id)])
      .then(([loadedSets, currentListing]) => {
        setSets(loadedSets);
        setListing(currentListing);
      })
      .catch(() => setError("Failed to load listing."));
  }, [params.id]);

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Edit Listing</h1>
      {error && <p className="text-red-600">{error}</p>}
      {sets.length > 0 && listing && <ListingForm sets={sets} listing={listing} />}
    </section>
  );
}
