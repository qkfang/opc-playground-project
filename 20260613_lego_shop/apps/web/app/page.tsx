import Link from "next/link";
import { listProducts, listThemes } from "@/lib/catalog";
import ProductCard from "@/components/ProductCard";

export default function HomePage() {
  const featured = listProducts({ featured: true });
  const themes = listThemes();

  return (
    <div className="space-y-12">
      <section className="overflow-hidden rounded-[2.5rem] bg-gradient-to-br from-red-700 via-orange-500 to-amber-300 px-8 py-14 text-white shadow-lg">
        <p className="text-sm font-semibold uppercase tracking-[0.3em] text-amber-50">
          Brick Bazaar
        </p>
        <h1 className="mt-3 max-w-2xl text-4xl font-bold leading-tight sm:text-5xl">
          Build something brilliant.
        </h1>
        <p className="mt-4 max-w-xl text-base leading-7 text-amber-50 sm:text-lg">
          A curated catalog of Lego sets across City, Technic, Creator, and Space.
          Browse, add to cart, and start your next build.
        </p>
        <div className="mt-7 flex flex-wrap gap-3">
          <Link
            href="/shop"
            className="rounded-full bg-white px-6 py-3 text-sm font-semibold text-slate-900 shadow-sm hover:bg-slate-100"
          >
            Shop all sets
          </Link>
          <Link
            href="/cart"
            className="rounded-full border border-white/60 px-6 py-3 text-sm font-semibold text-white hover:bg-white/10"
          >
            View cart
          </Link>
        </div>
      </section>

      <section>
        <div className="flex items-end justify-between">
          <h2 className="text-2xl font-bold text-slate-900">Featured sets</h2>
          <Link href="/shop" className="text-sm font-semibold text-slate-700 hover:underline">
            See all →
          </Link>
        </div>
        <div className="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {featured.map((product) => (
            <ProductCard key={product.id} product={product} />
          ))}
        </div>
      </section>

      <section className="rounded-[2rem] bg-white p-8 shadow-sm ring-1 ring-slate-100">
        <h2 className="text-2xl font-bold text-slate-900">Shop by theme</h2>
        <p className="mt-2 text-sm text-slate-600">
          Jump straight into a collection that fits the build you&apos;re after.
        </p>
        <div className="mt-5 flex flex-wrap gap-3">
          {themes.map((theme) => (
            <Link
              key={theme}
              href={`/shop?theme=${encodeURIComponent(theme)}`}
              className="rounded-full border border-slate-300 px-5 py-2 text-sm font-medium text-slate-700 hover:border-slate-900 hover:text-slate-900"
            >
              {theme}
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}
