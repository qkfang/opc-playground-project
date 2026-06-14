import SetDetailClient from "./client";

// Static export needs a known param set; emit one placeholder shell.
// SWA navigationFallback serves it for any real id (read client-side).
export function generateStaticParams() {
  return [{ id: "_" }];
}

export const dynamic = "force-static";

export default function SetDetailPage() {
  return <SetDetailClient />;
}
