import { proxyBackend } from "@/lib/backend-proxy";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  const { id } = await params;
  return proxyBackend(request, `/sets/${id}`);
}
