"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.resetDataStoreForTests = resetDataStoreForTests;
exports.getDataStore = getDataStore;
const cosmos_1 = require("@azure/cosmos");
const mock_data_1 = require("./mock-data");
class MockDataStore {
    dataSource = "mock";
    sets = [...mock_data_1.defaultSets];
    listings = [...mock_data_1.defaultListings];
    async listSets() {
        return this.sets;
    }
    async getSetById(id) {
        return this.sets.find((set) => set.id === id);
    }
    async listListings(owner) {
        const items = owner
            ? this.listings.filter((listing) => listing.sellerUserId === owner)
            : this.listings;
        return [...items].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    }
    async getListingById(id) {
        return this.listings.find((listing) => listing.id === id);
    }
    async createListing(input, sellerUserId) {
        const created = {
            ...input,
            id: `listing-${Date.now()}`,
            sellerUserId,
            createdAt: new Date().toISOString(),
        };
        this.listings.unshift(created);
        return created;
    }
    async updateListing(id, input) {
        const index = this.listings.findIndex((listing) => listing.id === id);
        if (index < 0) {
            return undefined;
        }
        const updated = {
            ...this.listings[index],
            ...input,
        };
        this.listings[index] = updated;
        return updated;
    }
    async deleteListing(id) {
        const index = this.listings.findIndex((listing) => listing.id === id);
        if (index < 0) {
            return false;
        }
        this.listings.splice(index, 1);
        return true;
    }
}
class CosmosDataStore {
    container;
    dataSource = "cosmos";
    seedPromise;
    constructor(container) {
        this.container = container;
        this.seedPromise = this.seedData();
    }
    async seedData() {
        const setCount = await this.countDocuments("set");
        if (setCount === 0) {
            for (const set of (0, mock_data_1.getSeedSetDocuments)()) {
                await this.container.items.upsert(set);
            }
        }
        const listingCount = await this.countDocuments("listing");
        if (listingCount === 0) {
            for (const listing of (0, mock_data_1.getSeedListingDocuments)()) {
                await this.container.items.upsert(listing);
            }
        }
    }
    async countDocuments(type) {
        const { resources } = await this.container.items
            .query({
            query: "SELECT VALUE COUNT(1) FROM c WHERE c.type = @type",
            parameters: [{ name: "@type", value: type }],
        })
            .fetchAll();
        return resources[0] ?? 0;
    }
    async ensureSeeded() {
        await this.seedPromise;
    }
    async listSets() {
        await this.ensureSeeded();
        const { resources } = await this.container.items
            .query({
            query: "SELECT * FROM c WHERE c.type = @type ORDER BY c.name ASC",
            parameters: [{ name: "@type", value: "set" }],
        })
            .fetchAll();
        return resources.map(stripDocumentType);
    }
    async getSetById(id) {
        await this.ensureSeeded();
        const { resources } = await this.container.items
            .query({
            query: "SELECT * FROM c WHERE c.type = @type AND c.id = @id",
            parameters: [
                { name: "@type", value: "set" },
                { name: "@id", value: id },
            ],
        })
            .fetchAll();
        return resources[0] ? stripDocumentType(resources[0]) : undefined;
    }
    async listListings(owner) {
        await this.ensureSeeded();
        const parameters = [{ name: "@type", value: "listing" }];
        let query = "SELECT * FROM c WHERE c.type = @type";
        if (owner) {
            query += " AND c.sellerUserId = @owner";
            parameters.push({ name: "@owner", value: owner });
        }
        query += " ORDER BY c.createdAt DESC";
        const { resources } = await this.container.items
            .query({ query, parameters })
            .fetchAll();
        return resources.map(stripDocumentType);
    }
    async getListingById(id) {
        await this.ensureSeeded();
        const { resources } = await this.container.items
            .query({
            query: "SELECT * FROM c WHERE c.type = @type AND c.id = @id",
            parameters: [
                { name: "@type", value: "listing" },
                { name: "@id", value: id },
            ],
        })
            .fetchAll();
        return resources[0] ? stripDocumentType(resources[0]) : undefined;
    }
    async createListing(input, sellerUserId) {
        await this.ensureSeeded();
        const created = {
            ...input,
            type: "listing",
            id: `listing-${Date.now()}`,
            sellerUserId,
            createdAt: new Date().toISOString(),
        };
        const { resource } = await this.container.items.create(created);
        return stripDocumentType(resource ?? created);
    }
    async updateListing(id, input) {
        await this.ensureSeeded();
        const current = await this.getListingById(id);
        if (!current) {
            return undefined;
        }
        const updated = {
            ...current,
            ...input,
            type: "listing",
        };
        const { resource } = await this.container.item(id, "listing").replace(updated);
        return stripDocumentType(resource ?? updated);
    }
    async deleteListing(id) {
        await this.ensureSeeded();
        const current = await this.getListingById(id);
        if (!current) {
            return false;
        }
        await this.container.item(id, "listing").delete();
        return true;
    }
}
function stripDocumentType(item) {
    const { type: _type, ...rest } = item;
    return rest;
}
let storePromise;
function resetDataStoreForTests() {
    storePromise = undefined;
}
function getDataStore() {
    if (!storePromise) {
        storePromise = createDataStore();
    }
    return storePromise;
}
async function createDataStore() {
    const connectionString = process.env.COSMOS_CONNECTION_STRING;
    const databaseName = process.env.COSMOS_DATABASE_NAME;
    const containerName = process.env.COSMOS_CONTAINER_NAME;
    if (!connectionString || !databaseName || !containerName) {
        return new MockDataStore();
    }
    const client = new cosmos_1.CosmosClient(connectionString);
    const { database } = await client.databases.createIfNotExists({ id: databaseName });
    const { container } = await database.containers.createIfNotExists({
        id: containerName,
        partitionKey: { paths: ["/type"] },
    });
    return new CosmosDataStore(container);
}
//# sourceMappingURL=data-store.js.map