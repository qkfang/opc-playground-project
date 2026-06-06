"use client";

import { useEffect, useState } from "react";
import ListingForm from "@/app/components/listing-form";
import { fetchSets } from "@/lib/api";
import type { LegoSet } from "@/lib/types";

export default function CreateListingPage() {
  const [sets, setSets] = useState<LegoSet[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchSets().then(setSets).catch(() => setError("Failed to load sets."));
  }, []);

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Create Listing</h1>
      {error && <p className="text-red-600">{error}</p>}
      {sets.length > 0 && <ListingForm sets={sets} />}
    </section>
  );
}
