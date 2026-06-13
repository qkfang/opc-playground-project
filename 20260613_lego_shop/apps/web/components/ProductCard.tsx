import Link from "next/link";
import type { Product } from "@/lib/types";
import AddToCartButton from "./AddToCartButton";

function formatPrice(value: number): string {
  return new Intl.NumberFormat("en-AU", {
    style: "currency",
    currency: "AUD",
  }).format(value);
}

export default function ProductCard({ product }: { product: Product }) {
  return (
    <article className="flex flex-col overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-sm">
      <Link href={`/shop/${product.id}`} className="block">
        <div
          className={`relative flex h-40 items-end bg-gradient-to-br ${product.colorFrom} ${product.colorTo} p-4`}
        >
          <span className="rounded-full bg-white/85 px-3 py-1 text-xs font-semibold text-slate-800">
            {product.theme}
          </span>
        </div>
      </Link>
      <div className="flex flex-1 flex-col gap-3 p-5">
        <div>
          <Link
            href={`/shop/${product.id}`}
            className="font-semibold text-slate-900 hover:underline"
          >
            {product.name}
          </Link>
          <p className="mt-1 text-sm text-slate-600">{product.blurb}</p>
        </div>
        <div className="mt-auto flex items-center justify-between">
          <div>
            <p className="text-lg font-bold text-slate-900">
              {formatPrice(product.price)}
            </p>
            <p className="text-xs text-slate-500">
              {product.pieces} pcs · ⭐ {product.rating.toFixed(1)}
            </p>
          </div>
          <AddToCartButton productId={product.id} label="Add" />
        </div>
      </div>
    </article>
  );
}
