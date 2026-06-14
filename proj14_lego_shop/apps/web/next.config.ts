import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Live app with API route handlers (no static export).
  allowedDevOrigins: ["127.0.0.1"],
  // Produce a self-contained server bundle (.next/standalone/server.js) so we can
  // build in CI and deploy a prebuilt package to Azure App Service without an
  // on-server Oryx build.
  output: "standalone",
};

export default nextConfig;
