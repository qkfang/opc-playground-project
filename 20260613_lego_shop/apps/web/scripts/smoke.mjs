// Smoke test for the Lego Shop API.
// Usage: node scripts/smoke.mjs [baseUrl]
// Defaults to http://localhost:3000

const base = process.argv[2] ?? "http://localhost:3000";

let failures = 0;
function check(name, cond, extra = "") {
  if (cond) {
    console.log(`  ok   ${name}`);
  } else {
    failures++;
    console.log(`  FAIL ${name} ${extra}`);
  }
}

// A cookie jar so cart identity persists across requests.
let cookie = "";
async function api(path, init = {}) {
  const headers = { ...(init.headers || {}) };
  if (cookie) headers["Cookie"] = cookie;
  const res = await fetch(base + path, { ...init, headers });
  const setCookie = res.headers.get("set-cookie");
  if (setCookie) cookie = setCookie.split(";")[0];
  let body = null;
  try {
    body = await res.json();
  } catch {
    /* ignore */
  }
  return { res, body };
}

async function main() {
  console.log(`Smoke testing Lego Shop API at ${base}`);

  // health
  {
    const { res, body } = await api("/api/health");
    check("GET /api/health -> 200", res.status === 200);
    check("health status ok", body?.status === "ok");
    check("health reports products", typeof body?.products === "number" && body.products > 0);
  }

  // products list
  let firstId = null;
  {
    const { res, body } = await api("/api/products");
    check("GET /api/products -> 200", res.status === 200);
    check("products is non-empty array", Array.isArray(body?.products) && body.products.length > 0);
    check("themes is non-empty array", Array.isArray(body?.themes) && body.themes.length > 0);
    firstId = body?.products?.[0]?.id ?? null;
  }

  // products filtered by theme
  {
    const { res, body } = await api("/api/products?theme=City");
    check("GET /api/products?theme=City -> 200", res.status === 200);
    check("filtered list only City", Array.isArray(body?.products) && body.products.every((p) => p.theme === "City"));
  }

  // featured
  {
    const { res, body } = await api("/api/products?featured=true");
    check("GET /api/products?featured=true -> 200", res.status === 200);
    check("featured list only featured", Array.isArray(body?.products) && body.products.every((p) => p.featured === true));
  }

  // product detail
  {
    const { res, body } = await api(`/api/products/${firstId}`);
    check("GET /api/products/:id -> 200", res.status === 200);
    check("detail returns the product", body?.product?.id === firstId);
  }

  // product detail 404
  {
    const { res } = await api("/api/products/does-not-exist");
    check("GET /api/products/<missing> -> 404", res.status === 404);
  }

  // cart lifecycle
  {
    let { res, body } = await api("/api/cart");
    check("GET /api/cart -> 200", res.status === 200);
    check("new cart empty", body?.cart?.itemCount === 0);

    ({ res, body } = await api("/api/cart", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ productId: firstId, quantity: 2 }),
    }));
    check("POST /api/cart add -> 200", res.status === 200);
    check("cart has 2 items", body?.cart?.itemCount === 2);
    check("subtotal > 0", body?.cart?.subtotal > 0);

    ({ res, body } = await api("/api/cart", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ productId: firstId, quantity: 5 }),
    }));
    check("PATCH /api/cart qty -> 200", res.status === 200);
    check("cart qty now 5", body?.cart?.itemCount === 5);

    ({ res, body } = await api("/api/cart", {
      method: "DELETE",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ productId: firstId }),
    }));
    check("DELETE /api/cart line -> 200", res.status === 200);
    check("cart empty after remove", body?.cart?.itemCount === 0);

    // invalid product
    ({ res } = await api("/api/cart", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ productId: "nope" }),
    }));
    check("POST invalid product -> 400", res.status === 400);
  }

  console.log("");
  if (failures > 0) {
    console.log(`SMOKE FAILED: ${failures} check(s) failed`);
    process.exit(1);
  }
  console.log("SMOKE PASSED: all checks green");
}

main().catch((err) => {
  console.error("Smoke run error:", err);
  process.exit(1);
});
