import { proxyBackend } from "@/lib/backend-proxy";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const owner = searchParams.get("owner") ?? undefined;
  const query = owner ? `?owner=${encodeURIComponent(owner)}` : "";
  return proxyBackend(request, `/listings${query}`);
}

export async function POST(request: Request) {
  return proxyBackend(request, "/listings");
}
