import { NextResponse } from "next/server";
import { PRODUCTS } from "@/lib/catalog";

export const dynamic = "force-dynamic";

export async function GET() {
  return NextResponse.json({ status: "ok", products: PRODUCTS.length });
}
