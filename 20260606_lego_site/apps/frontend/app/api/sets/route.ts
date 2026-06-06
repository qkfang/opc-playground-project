import { NextResponse } from "next/server";
import { getSets } from "@/lib/data-store";

export async function GET() {
  return NextResponse.json(getSets());
}
