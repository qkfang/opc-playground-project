import ListingDetailClient from "./client";

export function generateStaticParams() {
  return [{ id: "_" }];
}

export const dynamic = "force-static";

export default function ListingDetailPage() {
  return <ListingDetailClient />;
}
