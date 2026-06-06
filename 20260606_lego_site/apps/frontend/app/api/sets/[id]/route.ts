import { NextResponse } from "next/server";
import { getSetById } from "@/lib/data-store";

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  const { id } = await params;
  const set = getSetById(id);

  if (!set) {
    return NextResponse.json({ message: "Set not found" }, { status: 404 });
  }

  return NextResponse.json(set);
}
