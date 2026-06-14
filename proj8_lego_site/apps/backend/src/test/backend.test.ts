import test from "node:test";
import assert from "node:assert/strict";
import { HttpRequest, InvocationContext } from "@azure/functions";
import { resetDataStoreForTests } from "../data-store";
import { createListing, deleteListing, getListingById, getListings, updateListing } from "../functions/listings";
import { getSetById as getSingleSet, getSets as listSets } from "../functions/sets";

function createRequest(method: string, url: string, init?: { body?: unknown; headers?: Record<string, string>; params?: Record<string, string> }) {
  return new HttpRequest({
    method,
    url,
    body: init?.body ? { string: JSON.stringify(init.body) } : undefined,
    headers: init?.headers,
    params: init?.params,
  });
}

function createContext(): InvocationContext {
  return new InvocationContext({ functionName: "test" });
}

test.beforeEach(() => {
  delete process.env.COSMOS_CONNECTION_STRING;
  delete process.env.COSMOS_DATABASE_NAME;
  delete process.env.COSMOS_CONTAINER_NAME;
  process.env.ALLOW_LOCAL_DEV_AUTH = "true";
  resetDataStoreForTests();
});

test("GET /api/sets returns seeded sets from mock store", async () => {
  const response = await listSets(createRequest("GET", "http://localhost/api/sets"), createContext());
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("x-data-source"), "mock");
  const sets = (await response.json()) as Array<{ id: string }>;
  assert.ok(sets.some((set) => set.id === "10326"));
});

test("GET /api/sets/{id} returns 404 for unknown set", async () => {
  const response = await getSingleSet(
    createRequest("GET", "http://localhost/api/sets/missing", { params: { id: "missing" } }),
    createContext(),
  );
  assert.equal(response.status, 404);
});

test("listing CRUD enforces auth and owner checks", async () => {
  const unauthenticatedCreate = await createListing(
    createRequest("POST", "http://localhost/api/listings", {
      body: {
        setId: "10326",
        title: "Test listing",
        condition: "new",
        price: 15,
        currency: "usd",
        description: "Test",
        status: "active",
      },
    }),
    createContext(),
  );
  assert.equal(unauthenticatedCreate.status, 401);

  const createdResponse = await createListing(
    createRequest("POST", "http://localhost/api/listings", {
      headers: { "x-user-id": "demo-user" },
      body: {
        setId: "10326",
        title: "Test listing",
        condition: "new",
        price: 15,
        currency: "usd",
        description: "Test",
        status: "active",
      },
    }),
    createContext(),
  );
  assert.equal(createdResponse.status, 201);
  const created = (await createdResponse.json()) as { id: string; sellerUserId: string; currency: string };
  assert.equal(created.sellerUserId, "demo-user");
  assert.equal(created.currency, "USD");

  const listResponse = await getListings(createRequest("GET", "http://localhost/api/listings?owner=demo-user"), createContext());
  const listings = (await listResponse.json()) as Array<{ id: string }>;
  assert.ok(listings.some((listing) => listing.id === created.id));

  const forbiddenUpdate = await updateListing(
    createRequest("PUT", `http://localhost/api/listings/${created.id}`, {
      params: { id: created.id },
      headers: { "x-user-id": "someone-else" },
      body: {
        setId: "10326",
        title: "Hacked",
        condition: "new",
        price: 20,
        currency: "USD",
        description: "Nope",
        status: "active",
      },
    }),
    createContext(),
  );
  assert.equal(forbiddenUpdate.status, 403);

  const updatedResponse = await updateListing(
    createRequest("PUT", `http://localhost/api/listings/${created.id}`, {
      params: { id: created.id },
      headers: { "x-user-id": "demo-user" },
      body: {
        setId: "10326",
        title: "Updated title",
        condition: "used-like-new",
        price: 25,
        currency: "USD",
        description: "Updated",
        status: "sold",
      },
    }),
    createContext(),
  );
  assert.equal(updatedResponse.status, 200);
  const updated = (await updatedResponse.json()) as { title: string; status: string };
  assert.equal(updated.title, "Updated title");
  assert.equal(updated.status, "sold");

  const deleteResponse = await deleteListing(
    createRequest("DELETE", `http://localhost/api/listings/${created.id}`, {
      params: { id: created.id },
      headers: { "x-user-id": "demo-user" },
    }),
    createContext(),
  );
  assert.equal(deleteResponse.status, 204);

  const missingResponse = await getListingById(
    createRequest("GET", `http://localhost/api/listings/${created.id}`, { params: { id: created.id } }),
    createContext(),
  );
  assert.equal(missingResponse.status, 404);
});
