import { NextResponse } from "next/server";
import {
  resolveCartId,
  getCartView,
  addToCart,
  setQuantity,
  removeFromCart,
  clearCart,
} from "@/lib/cart-store";

export const dynamic = "force-dynamic";

export async function GET() {
  const id = await resolveCartId();
  return NextResponse.json({ cart: getCartView(id) });
}

export async function POST(request: Request) {
  const id = await resolveCartId();
  let body: { productId?: string; quantity?: number };
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "invalid_body" }, { status: 400 });
  }
  if (!body.productId) {
    return NextResponse.json({ error: "invalid_product" }, { status: 400 });
  }
  const cart = addToCart(id, body.productId, body.quantity ?? 1);
  if (!cart) {
    return NextResponse.json({ error: "invalid_product" }, { status: 400 });
  }
  return NextResponse.json({ cart });
}

export async function PATCH(request: Request) {
  const id = await resolveCartId();
  let body: { productId?: string; quantity?: number };
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "invalid_body" }, { status: 400 });
  }
  if (!body.productId || typeof body.quantity !== "number") {
    return NextResponse.json({ error: "invalid_request" }, { status: 400 });
  }
  const cart = setQuantity(id, body.productId, body.quantity);
  return NextResponse.json({ cart });
}

export async function DELETE(request: Request) {
  const id = await resolveCartId();
  let body: { productId?: string; clear?: boolean };
  try {
    body = await request.json();
  } catch {
    body = {};
  }
  if (body.clear) {
    return NextResponse.json({ cart: clearCart(id) });
  }
  if (!body.productId) {
    return NextResponse.json({ error: "invalid_request" }, { status: 400 });
  }
  return NextResponse.json({ cart: removeFromCart(id, body.productId) });
}
