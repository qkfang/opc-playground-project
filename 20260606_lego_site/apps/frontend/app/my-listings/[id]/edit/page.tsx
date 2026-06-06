import EditListingClient from "./client";

export function generateStaticParams() {
  return [{ id: "_" }];
}

export const dynamic = "force-static";

export default function EditListingPage() {
  return <EditListingClient />;
}
