import Link from "next/link";
import { listProducts, listThemes } from "@/lib/catalog";
import ProductCard from "@/components/ProductCard";

export const dynamic = "force-dynamic";

export default async function ShopPage({
  searchParams,
}: {
  searchParams: Promise<{ theme?: string }>;
}) {
  const { theme } = await searchParams;
  const themes = listThemes();
  const activeTheme =
    theme && themes.some((t) => t.toLowerCase() === theme.toLowerCase())
      ? themes.find((t) => t.toLowerCase() === theme.toLowerCase())!
      : null;
  const products = listProducts({ theme: activeTheme });

  return (
    <div className="space-y-8">
      <header className="space-y-2">
        <h1 className="text-3xl font-bold text-slate-900">Shop all sets</h1>
        <p className="text-sm text-slate-600">
          {products.length} set{products.length === 1 ? "" : "s"}
          {activeTheme ? ` in ${activeTheme}` : " available"}.
        </p>
      </header>

      <div className="flex flex-wrap gap-3">
        <Link
          href="/shop"
          className={`rounded-full px-5 py-2 text-sm font-medium ${
            activeTheme
              ? "border border-slate-300 text-slate-700 hover:border-slate-900"
              : "bg-slate-900 text-white"
          }`}
        >
          All
        </Link>
        {themes.map((t) => {
          const active = activeTheme === t;
          return (
            <Link
              key={t}
              href={`/shop?theme=${encodeURIComponent(t)}`}
              className={`rounded-full px-5 py-2 text-sm font-medium ${
                active
                  ? "bg-slate-900 text-white"
                  : "border border-slate-300 text-slate-700 hover:border-slate-900 hover:text-slate-900"
              }`}
            >
              {t}
            </Link>
          );
        })}
      </div>

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {products.map((product) => (
          <ProductCard key={product.id} product={product} />
        ))}
      </div>
    </div>
  );
}
