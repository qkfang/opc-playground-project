import Link from "next/link";
import { notFound } from "next/navigation";
import { getProduct, listProducts, PRODUCTS } from "@/lib/catalog";
import AddToCartButton from "@/components/AddToCartButton";
import ProductCard from "@/components/ProductCard";

function formatPrice(value: number): string {
  return new Intl.NumberFormat("en-AU", {
    style: "currency",
    currency: "AUD",
  }).format(value);
}

export function generateStaticParams() {
  return PRODUCTS.map((p) => ({ id: p.id }));
}

export default async function ProductDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const product = getProduct(id);
  if (!product) {
    notFound();
  }

  const related = listProducts({ theme: product.theme })
    .filter((p) => p.id !== product.id)
    .slice(0, 3);

  return (
    <div className="space-y-12">
      <nav className="text-sm text-slate-500">
        <Link href="/shop" className="hover:underline">
          Shop
        </Link>
        <span className="px-2">/</span>
        <span className="text-slate-700">{product.name}</span>
      </nav>

      <div className="grid gap-8 lg:grid-cols-2">
        <div
          className={`flex min-h-72 items-end rounded-[2rem] bg-gradient-to-br ${product.colorFrom} ${product.colorTo} p-6 text-white shadow-lg`}
        >
          <span className="rounded-full bg-white/85 px-4 py-1.5 text-sm font-semibold text-slate-800">
            {product.theme}
          </span>
        </div>

        <div className="flex flex-col gap-5">
          <div>
            <h1 className="text-3xl font-bold text-slate-900">{product.name}</h1>
            <p className="mt-2 text-slate-600">{product.blurb}</p>
          </div>

          <p className="text-3xl font-bold text-slate-900">
            {formatPrice(product.price)}
          </p>

          <dl className="grid grid-cols-2 gap-3 text-sm">
            <div className="rounded-2xl bg-white p-4 ring-1 ring-slate-100">
              <dt className="text-slate-500">Pieces</dt>
              <dd className="text-lg font-semibold text-slate-900">{product.pieces}</dd>
            </div>
            <div className="rounded-2xl bg-white p-4 ring-1 ring-slate-100">
              <dt className="text-slate-500">Age</dt>
              <dd className="text-lg font-semibold text-slate-900">{product.ageRange}</dd>
            </div>
            <div className="rounded-2xl bg-white p-4 ring-1 ring-slate-100">
              <dt className="text-slate-500">Rating</dt>
              <dd className="text-lg font-semibold text-slate-900">
                ⭐ {product.rating.toFixed(1)}
              </dd>
            </div>
            <div className="rounded-2xl bg-white p-4 ring-1 ring-slate-100">
              <dt className="text-slate-500">Theme</dt>
              <dd className="text-lg font-semibold text-slate-900">{product.theme}</dd>
            </div>
          </dl>

          <p className="leading-7 text-slate-700">{product.description}</p>

          <div className="flex gap-3">
            <AddToCartButton productId={product.id} label="Add to cart" />
            <Link
              href="/cart"
              className="inline-flex items-center justify-center rounded-full border border-slate-300 px-5 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
            >
              Go to cart
            </Link>
          </div>
        </div>
      </div>

      {related.length > 0 && (
        <section>
          <h2 className="text-2xl font-bold text-slate-900">More in {product.theme}</h2>
          <div className="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {related.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
