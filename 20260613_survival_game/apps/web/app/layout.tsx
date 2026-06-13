import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Last Stand — Survival Game",
  description:
    "A fast, replayable browser survival game with multiple distinct modes: Classic, Blitz, and Nightfall.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
