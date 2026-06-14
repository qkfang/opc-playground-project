import { NextResponse } from "next/server";
import { listProducts, listThemes } from "@/lib/catalog";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const theme = searchParams.get("theme");
  const featured = searchParams.get("featured") === "true";

  const products = listProducts({ theme, featured });
  return NextResponse.json({ products, themes: listThemes() });
}
