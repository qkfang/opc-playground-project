"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { fetchSets } from "@/lib/api";
import type { LegoSet } from "@/lib/types";

export default function SetsPage() {
  const [sets, setSets] = useState<LegoSet[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchSets().then(setSets).catch(() => setError("Failed to load sets."));
  }, []);

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Browse Sets</h1>
      {error && <p className="text-red-600">{error}</p>}
      <ul className="space-y-3">
        {sets.map((set) => (
          <li key={set.id} className="rounded border bg-white p-4">
            <h2 className="text-lg font-medium">{set.name}</h2>
            <p className="text-sm text-slate-600">
              {set.theme} • {set.year} • {set.pieces} pieces
            </p>
            <Link className="mt-2 inline-block text-blue-700" href={`/sets/${set.id}`}>
              View set details
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
