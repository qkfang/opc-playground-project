import type { Metadata } from "next";
import Link from "next/link";
import AuthStatus from "./components/auth-status";
import "./globals.css";

export const metadata: Metadata = {
  title: "LEGO Marketplace",
  description: "Browse LEGO sets and marketplace listings.",
};

const navItems = [
  { href: "/", label: "Home" },
  { href: "/sets", label: "Browse Sets" },
  { href: "/marketplace", label: "Marketplace" },
  { href: "/bird", label: "Bird" },
  { href: "/pig", label: "Pig" },
  { href: "/hat", label: "Hat" },
  { href: "/ball", label: "Ball" },
  { href: "/readme", label: "README" },
  { href: "/about3", label: "About3" },
  { href: "/about4", label: "About4" },
  { href: "/about5", label: "About5" },
  { href: "/about6", label: "About6" },
  { href: "/about7", label: "About7" },
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
          <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-4 px-4 py-4 text-sm font-medium">
            <nav className="flex flex-wrap gap-4">
              {navItems.map((item) => (
                <Link key={item.href} href={item.href} className="hover:text-blue-700">
                  {item.label}
                </Link>
              ))}
            </nav>
            <AuthStatus />
          </div>
        </header>
        <main className="mx-auto max-w-5xl px-4 py-8">{children}</main>
      </body>
    </html>
  );
}
