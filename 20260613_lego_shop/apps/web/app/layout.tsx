import type { Metadata } from "next";
import Link from "next/link";
import CartBadge from "@/components/CartBadge";
import "./globals.css";

export const metadata: Metadata = {
  title: "Brick Bazaar — Lego Shop",
  description: "Browse and shop a curated catalog of Lego sets.",
};

const navItems = [
  { href: "/", label: "Home" },
  { href: "/shop", label: "Shop" },
];

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body className="min-h-screen">
        <header className="border-b border-slate-200 bg-white">
          <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-6 py-4">
            <Link href="/" className="flex items-center gap-2 font-bold text-slate-900">
              <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-red-600 to-amber-400 text-white">
                🧱
              </span>
              Brick Bazaar
            </Link>
            <nav className="flex items-center gap-5 text-sm font-medium text-slate-700">
              {navItems.map((item) => (
                <Link key={item.href} href={item.href} className="hover:text-slate-950">
                  {item.label}
                </Link>
              ))}
              <Link href="/cart" className="hover:text-slate-950">
                <CartBadge />
              </Link>
            </nav>
          </div>
        </header>

        <main className="mx-auto max-w-6xl px-6 py-10">{children}</main>

        <footer className="border-t border-slate-200 bg-white">
          <div className="mx-auto max-w-6xl px-6 py-6 text-sm text-slate-500">
            Brick Bazaar · MVP storefront · built by the Build Team
          </div>
        </footer>
      </body>
    </html>
  );
}
