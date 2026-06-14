import CartControls from "@/components/CartControls";

export const metadata = {
  title: "Your cart — Brick Bazaar",
};

export default function CartPage() {
  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-3xl font-bold text-slate-900">Your cart</h1>
        <p className="mt-2 text-sm text-slate-600">
          Review your sets, adjust quantities, and head to checkout.
        </p>
      </header>
      <CartControls />
    </div>
  );
}
