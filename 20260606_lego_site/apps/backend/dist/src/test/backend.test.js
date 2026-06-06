"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const node_test_1 = __importDefault(require("node:test"));
const strict_1 = __importDefault(require("node:assert/strict"));
const functions_1 = require("@azure/functions");
const data_store_1 = require("../data-store");
const listings_1 = require("../functions/listings");
const sets_1 = require("../functions/sets");
function createRequest(method, url, init) {
    return new functions_1.HttpRequest({
        method,
        url,
        body: init?.body ? { string: JSON.stringify(init.body) } : undefined,
        headers: init?.headers,
        params: init?.params,
    });
}
function createContext() {
    return new functions_1.InvocationContext({ functionName: "test" });
}
node_test_1.default.beforeEach(() => {
    delete process.env.COSMOS_CONNECTION_STRING;
    delete process.env.COSMOS_DATABASE_NAME;
    delete process.env.COSMOS_CONTAINER_NAME;
    process.env.ALLOW_LOCAL_DEV_AUTH = "true";
    (0, data_store_1.resetDataStoreForTests)();
});
(0, node_test_1.default)("GET /api/sets returns seeded sets from mock store", async () => {
    const response = await (0, sets_1.getSets)(createRequest("GET", "http://localhost/api/sets"), createContext());
    strict_1.default.equal(response.status, 200);
    strict_1.default.equal(response.headers.get("x-data-source"), "mock");
    const sets = (await response.json());
    strict_1.default.ok(sets.some((set) => set.id === "10326"));
});
(0, node_test_1.default)("GET /api/sets/{id} returns 404 for unknown set", async () => {
    const response = await (0, sets_1.getSetById)(createRequest("GET", "http://localhost/api/sets/missing", { params: { id: "missing" } }), createContext());
    strict_1.default.equal(response.status, 404);
});
(0, node_test_1.default)("listing CRUD enforces auth and owner checks", async () => {
    const unauthenticatedCreate = await (0, listings_1.createListing)(createRequest("POST", "http://localhost/api/listings", {
        body: {
            setId: "10326",
            title: "Test listing",
            condition: "new",
            price: 15,
            currency: "usd",
            description: "Test",
            status: "active",
        },
    }), createContext());
    strict_1.default.equal(unauthenticatedCreate.status, 401);
    const createdResponse = await (0, listings_1.createListing)(createRequest("POST", "http://localhost/api/listings", {
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
    }), createContext());
    strict_1.default.equal(createdResponse.status, 201);
    const created = (await createdResponse.json());
    strict_1.default.equal(created.sellerUserId, "demo-user");
    strict_1.default.equal(created.currency, "USD");
    const listResponse = await (0, listings_1.getListings)(createRequest("GET", "http://localhost/api/listings?owner=demo-user"), createContext());
    const listings = (await listResponse.json());
    strict_1.default.ok(listings.some((listing) => listing.id === created.id));
    const forbiddenUpdate = await (0, listings_1.updateListing)(createRequest("PUT", `http://localhost/api/listings/${created.id}`, {
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
    }), createContext());
    strict_1.default.equal(forbiddenUpdate.status, 403);
    const updatedResponse = await (0, listings_1.updateListing)(createRequest("PUT", `http://localhost/api/listings/${created.id}`, {
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
    }), createContext());
    strict_1.default.equal(updatedResponse.status, 200);
    const updated = (await updatedResponse.json());
    strict_1.default.equal(updated.title, "Updated title");
    strict_1.default.equal(updated.status, "sold");
    const deleteResponse = await (0, listings_1.deleteListing)(createRequest("DELETE", `http://localhost/api/listings/${created.id}`, {
        params: { id: created.id },
        headers: { "x-user-id": "demo-user" },
    }), createContext());
    strict_1.default.equal(deleteResponse.status, 204);
    const missingResponse = await (0, listings_1.getListingById)(createRequest("GET", `http://localhost/api/listings/${created.id}`, { params: { id: created.id } }), createContext());
    strict_1.default.equal(missingResponse.status, 404);
});
//# sourceMappingURL=backend.test.js.map