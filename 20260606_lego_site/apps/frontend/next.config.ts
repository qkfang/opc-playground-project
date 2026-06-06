import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "export",
  images: { unoptimized: true },
  // Dynamic [id] routes are client-rendered SPAs; emit a placeholder shell and
  // let SWA navigationFallback serve it for any id (real id read client-side).
  trailingSlash: true,
};

export default nextConfig;
