import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Fully static export — the survival game is 100% client-side (no API/backend),
  // so it ships as static assets to Azure Static Web Apps.
  output: "export",
  images: { unoptimized: true },
  trailingSlash: true,
  allowedDevOrigins: ["127.0.0.1"],
};

export default nextConfig;
