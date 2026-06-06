import { NextResponse } from "next/server";
import { createListing, getListings } from "@/lib/data-store";
import type { ListingInput } from "@/lib/types";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const owner = searchParams.get("owner") ?? undefined;

  return NextResponse.json(getListings(owner));
}

export async function POST(request: Request) {
  const input = (await request.json()) as ListingInput;
  return NextResponse.json(createListing(input), { status: 201 });
}
