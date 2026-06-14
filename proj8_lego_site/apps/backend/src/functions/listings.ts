import { app, type HttpRequest, type InvocationContext } from "@azure/functions";
import { getAuthenticatedUser } from "../auth";
import { getDataStore } from "../data-store";
import { badRequest, emptyResponse, jsonResponse, parseListingMutation } from "../http";

export async function getListings(request: HttpRequest, _context: InvocationContext): Promise<Response> {
  const store = await getDataStore();
  const owner = new URL(request.url).searchParams.get("owner") ?? undefined;
  return jsonResponse(200, await store.listListings(owner), store.dataSource);
}

export async function getListingById(request: HttpRequest, _context: InvocationContext): Promise<Response> {
  const store = await getDataStore();
  const listing = await store.getListingById(request.params.id);

  if (!listing) {
    return jsonResponse(404, { message: "Listing not found" }, store.dataSource);
  }

  return jsonResponse(200, listing, store.dataSource);
}

export async function createListing(request: HttpRequest, _context: InvocationContext): Promise<Response> {
  const store = await getDataStore();
  const user = getAuthenticatedUser(request);

  if (!user) {
    return jsonResponse(401, { message: "Authentication required" }, store.dataSource);
  }

  try {
    const input = parseListingMutation(await request.json());
    const created = await store.createListing(input, user.userId);
    return jsonResponse(201, created, store.dataSource);
  } catch (error) {
    return badRequest(error instanceof Error ? error.message : "Invalid request body", store.dataSource);
  }
}

export async function updateListing(request: HttpRequest, _context: InvocationContext): Promise<Response> {
  const store = await getDataStore();
  const user = getAuthenticatedUser(request);

  if (!user) {
    return jsonResponse(401, { message: "Authentication required" }, store.dataSource);
  }

  const existing = await store.getListingById(request.params.id);
  if (!existing) {
    return jsonResponse(404, { message: "Listing not found" }, store.dataSource);
  }

  if (existing.sellerUserId !== user.userId) {
    return jsonResponse(403, { message: "You can only modify your own listings" }, store.dataSource);
  }

  try {
    const input = parseListingMutation(await request.json());
    const updated = await store.updateListing(request.params.id, input);
    return jsonResponse(200, updated, store.dataSource);
  } catch (error) {
    return badRequest(error instanceof Error ? error.message : "Invalid request body", store.dataSource);
  }
}

export async function deleteListing(request: HttpRequest, _context: InvocationContext): Promise<Response> {
  const store = await getDataStore();
  const user = getAuthenticatedUser(request);

  if (!user) {
    return jsonResponse(401, { message: "Authentication required" }, store.dataSource);
  }

  const existing = await store.getListingById(request.params.id);
  if (!existing) {
    return jsonResponse(404, { message: "Listing not found" }, store.dataSource);
  }

  if (existing.sellerUserId !== user.userId) {
    return jsonResponse(403, { message: "You can only modify your own listings" }, store.dataSource);
  }

  await store.deleteListing(request.params.id);
  return emptyResponse(204, store.dataSource);
}

app.http("listings-list", {
  route: "listings",
  methods: ["GET"],
  authLevel: "anonymous",
  handler: getListings,
});

app.http("listings-create", {
  route: "listings",
  methods: ["POST"],
  authLevel: "anonymous",
  handler: createListing,
});

app.http("listings-get", {
  route: "listings/{id}",
  methods: ["GET"],
  authLevel: "anonymous",
  handler: getListingById,
});

app.http("listings-update", {
  route: "listings/{id}",
  methods: ["PUT"],
  authLevel: "anonymous",
  handler: updateListing,
});

app.http("listings-delete", {
  route: "listings/{id}",
  methods: ["DELETE"],
  authLevel: "anonymous",
  handler: deleteListing,
});
