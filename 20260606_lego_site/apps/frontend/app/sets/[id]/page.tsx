"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { fetchSet } from "@/lib/api";
import type { LegoSet } from "@/lib/types";

export default function SetDetailPage() {
  const params = useParams<{ id: string }>();
  const [setData, setSetData] = useState<LegoSet | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!params.id) {
      return;
    }

    fetchSet(params.id).then(setSetData).catch(() => setError("Set not found."));
  }, [params.id]);

  return (
    <section className="space-y-4">
      <Link className="text-blue-700" href="/sets">
        ← Back to Browse Sets
      </Link>
      {error && <p className="text-red-600">{error}</p>}
      {setData && (
        <article className="rounded border bg-white p-4">
          <h1 className="text-2xl font-semibold">{setData.name}</h1>
          <p className="text-slate-700">Theme: {setData.theme}</p>
          <p className="text-slate-700">Release year: {setData.year}</p>
          <p className="text-slate-700">Pieces: {setData.pieces}</p>
        </article>
      )}
    </section>
  );
}
