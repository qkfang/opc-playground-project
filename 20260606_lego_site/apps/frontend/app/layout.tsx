import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "LEGO Marketplace",
  description: "Browse LEGO sets and marketplace listings.",
};

const navItems = [
  { href: "/", label: "Home" },
  { href: "/sets", label: "Browse Sets" },
  { href: "/marketplace", label: "Marketplace" },
  { href: "/my-listings", label: "My Listings" },
  { href: "/my-listings/new", label: "Create Listing" },
];

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="bg-slate-50 text-slate-900">
        <header className="border-b bg-white">
          <nav className="mx-auto flex max-w-5xl flex-wrap gap-4 px-4 py-4 text-sm font-medium">
            {navItems.map((item) => (
              <Link key={item.href} href={item.href} className="hover:text-blue-700">
                {item.label}
              </Link>
            ))}
          </nav>
        </header>
        <main className="mx-auto max-w-5xl px-4 py-8">{children}</main>
      </body>
    </html>
  );
}
