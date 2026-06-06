import Link from "next/link";

export default function HomePage() {
  return (
    <section className="space-y-4">
      <h1 className="text-3xl font-bold">LEGO Marketplace</h1>
      <p className="text-slate-700">
        Discover LEGO sets, browse active listings, and manage your own items.
      </p>
      <div className="flex gap-3">
        <Link
          className="rounded bg-blue-600 px-4 py-2 text-white"
          href="/sets"
        >
          Browse Sets
        </Link>
        <Link
          className="rounded border border-blue-600 px-4 py-2 text-blue-600"
          href="/marketplace"
        >
          View Marketplace
        </Link>
      </div>
    </section>
  );
}
