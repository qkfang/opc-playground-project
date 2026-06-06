const baseUrl = process.env.BACKEND_BASE_URL ?? "http://127.0.0.1:7071";
const authHeaders = process.env.SMOKE_USER_ID
  ? { "x-user-id": process.env.SMOKE_USER_ID }
  : { "x-user-id": "demo-user" };

async function request(path, init = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...authHeaders,
      ...(init.headers ?? {}),
    },
  });

  const text = await response.text();
  const json = text ? JSON.parse(text) : null;

  if (!response.ok) {
    throw new Error(`${init.method ?? "GET"} ${path} failed: ${response.status} ${text}`);
  }

  return { response, json };
}

const listingInput = {
  setId: "10326",
  title: "Smoke test listing",
  condition: "new",
  price: 100,
  currency: "USD",
  description: "Created by smoke test",
  status: "active",
};

const sets = await request("/api/sets");
if (!Array.isArray(sets.json) || sets.json.length === 0) {
  throw new Error("Expected at least one set");
}

const created = await request("/api/listings", {
  method: "POST",
  body: JSON.stringify(listingInput),
});

await request(`/api/listings/${created.json.id}`);
await request(`/api/listings/${created.json.id}`, {
  method: "PUT",
  body: JSON.stringify({ ...listingInput, title: "Updated smoke test listing" }),
});
await request(`/api/listings/${created.json.id}`, {
  method: "DELETE",
});

console.log("Smoke test passed", {
  dataSource: created.response.headers.get("x-data-source"),
  createdListingId: created.json.id,
});
