"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.getListings = getListings;
exports.getListingById = getListingById;
exports.createListing = createListing;
exports.updateListing = updateListing;
exports.deleteListing = deleteListing;
const functions_1 = require("@azure/functions");
const auth_1 = require("../auth");
const data_store_1 = require("../data-store");
const http_1 = require("../http");
async function getListings(request, _context) {
    const store = await (0, data_store_1.getDataStore)();
    const owner = new URL(request.url).searchParams.get("owner") ?? undefined;
    return (0, http_1.jsonResponse)(200, await store.listListings(owner), store.dataSource);
}
async function getListingById(request, _context) {
    const store = await (0, data_store_1.getDataStore)();
    const listing = await store.getListingById(request.params.id);
    if (!listing) {
        return (0, http_1.jsonResponse)(404, { message: "Listing not found" }, store.dataSource);
    }
    return (0, http_1.jsonResponse)(200, listing, store.dataSource);
}
async function createListing(request, _context) {
    const store = await (0, data_store_1.getDataStore)();
    const user = (0, auth_1.getAuthenticatedUser)(request);
    if (!user) {
        return (0, http_1.jsonResponse)(401, { message: "Authentication required" }, store.dataSource);
    }
    try {
        const input = (0, http_1.parseListingMutation)(await request.json());
        const created = await store.createListing(input, user.userId);
        return (0, http_1.jsonResponse)(201, created, store.dataSource);
    }
    catch (error) {
        return (0, http_1.badRequest)(error instanceof Error ? error.message : "Invalid request body", store.dataSource);
    }
}
async function updateListing(request, _context) {
    const store = await (0, data_store_1.getDataStore)();
    const user = (0, auth_1.getAuthenticatedUser)(request);
    if (!user) {
        return (0, http_1.jsonResponse)(401, { message: "Authentication required" }, store.dataSource);
    }
    const existing = await store.getListingById(request.params.id);
    if (!existing) {
        return (0, http_1.jsonResponse)(404, { message: "Listing not found" }, store.dataSource);
    }
    if (existing.sellerUserId !== user.userId) {
        return (0, http_1.jsonResponse)(403, { message: "You can only modify your own listings" }, store.dataSource);
    }
    try {
        const input = (0, http_1.parseListingMutation)(await request.json());
        const updated = await store.updateListing(request.params.id, input);
        return (0, http_1.jsonResponse)(200, updated, store.dataSource);
    }
    catch (error) {
        return (0, http_1.badRequest)(error instanceof Error ? error.message : "Invalid request body", store.dataSource);
    }
}
async function deleteListing(request, _context) {
    const store = await (0, data_store_1.getDataStore)();
    const user = (0, auth_1.getAuthenticatedUser)(request);
    if (!user) {
        return (0, http_1.jsonResponse)(401, { message: "Authentication required" }, store.dataSource);
    }
    const existing = await store.getListingById(request.params.id);
    if (!existing) {
        return (0, http_1.jsonResponse)(404, { message: "Listing not found" }, store.dataSource);
    }
    if (existing.sellerUserId !== user.userId) {
        return (0, http_1.jsonResponse)(403, { message: "You can only modify your own listings" }, store.dataSource);
    }
    await store.deleteListing(request.params.id);
    return (0, http_1.emptyResponse)(204, store.dataSource);
}
functions_1.app.http("listings-list", {
    route: "listings",
    methods: ["GET"],
    authLevel: "anonymous",
    handler: getListings,
});
functions_1.app.http("listings-create", {
    route: "listings",
    methods: ["POST"],
    authLevel: "anonymous",
    handler: createListing,
});
functions_1.app.http("listings-get", {
    route: "listings/{id}",
    methods: ["GET"],
    authLevel: "anonymous",
    handler: getListingById,
});
functions_1.app.http("listings-update", {
    route: "listings/{id}",
    methods: ["PUT"],
    authLevel: "anonymous",
    handler: updateListing,
});
functions_1.app.http("listings-delete", {
    route: "listings/{id}",
    methods: ["DELETE"],
    authLevel: "anonymous",
    handler: deleteListing,
});
//# sourceMappingURL=listings.js.map