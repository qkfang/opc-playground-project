import { CosmosClient, type Container } from "@azure/cosmos";
import { defaultListings, defaultSets, getSeedListingDocuments, getSeedSetDocuments } from "./mock-data";
import type { DataSource, LegoSet, Listing, ListingMutation, StoredListing, StoredSet } from "./types";

export interface DataStore {
  readonly dataSource: DataSource;
  listSets(): Promise<LegoSet[]>;
  getSetById(id: string): Promise<LegoSet | undefined>;
  listListings(owner?: string): Promise<Listing[]>;
  getListingById(id: string): Promise<Listing | undefined>;
  createListing(input: ListingMutation, sellerUserId: string): Promise<Listing>;
  updateListing(id: string, input: ListingMutation): Promise<Listing | undefined>;
  deleteListing(id: string): Promise<boolean>;
}

class MockDataStore implements DataStore {
  public readonly dataSource = "mock" as const;
  private readonly sets = [...defaultSets];
  private readonly listings = [...defaultListings];

  async listSets(): Promise<LegoSet[]> {
    return this.sets;
  }

  async getSetById(id: string): Promise<LegoSet | undefined> {
    return this.sets.find((set) => set.id === id);
  }

  async listListings(owner?: string): Promise<Listing[]> {
    const items = owner
      ? this.listings.filter((listing) => listing.sellerUserId === owner)
      : this.listings;

    return [...items].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }

  async getListingById(id: string): Promise<Listing | undefined> {
    return this.listings.find((listing) => listing.id === id);
  }

  async createListing(input: ListingMutation, sellerUserId: string): Promise<Listing> {
    const created: Listing = {
      ...input,
      id: `listing-${Date.now()}`,
      sellerUserId,
      createdAt: new Date().toISOString(),
    };

    this.listings.unshift(created);
    return created;
  }

  async updateListing(id: string, input: ListingMutation): Promise<Listing | undefined> {
    const index = this.listings.findIndex((listing) => listing.id === id);
    if (index < 0) {
      return undefined;
    }

    const updated: Listing = {
      ...this.listings[index],
      ...input,
    };

    this.listings[index] = updated;
    return updated;
  }

  async deleteListing(id: string): Promise<boolean> {
    const index = this.listings.findIndex((listing) => listing.id === id);
    if (index < 0) {
      return false;
    }

    this.listings.splice(index, 1);
    return true;
  }
}

class CosmosDataStore implements DataStore {
  public readonly dataSource = "cosmos" as const;
  private readonly seedPromise: Promise<void>;

  constructor(private readonly container: Container) {
    this.seedPromise = this.seedData();
  }

  private async seedData(): Promise<void> {
    const setCount = await this.countDocuments("set");
    if (setCount === 0) {
      for (const set of getSeedSetDocuments()) {
        await this.container.items.upsert(set);
      }
    }

    const listingCount = await this.countDocuments("listing");
    if (listingCount === 0) {
      for (const listing of getSeedListingDocuments()) {
        await this.container.items.upsert(listing);
      }
    }
  }

  private async countDocuments(type: "set" | "listing"): Promise<number> {
    const { resources } = await this.container.items
      .query<number>({
        query: "SELECT VALUE COUNT(1) FROM c WHERE c.type = @type",
        parameters: [{ name: "@type", value: type }],
      })
      .fetchAll();

    return resources[0] ?? 0;
  }

  private async ensureSeeded(): Promise<void> {
    await this.seedPromise;
  }

  async listSets(): Promise<LegoSet[]> {
    await this.ensureSeeded();
    const { resources } = await this.container.items
      .query<StoredSet>({
        query: "SELECT * FROM c WHERE c.type = @type ORDER BY c.name ASC",
        parameters: [{ name: "@type", value: "set" }],
      })
      .fetchAll();

    return resources.map(stripDocumentType);
  }

  async getSetById(id: string): Promise<LegoSet | undefined> {
    await this.ensureSeeded();
    const { resources } = await this.container.items
      .query<StoredSet>({
        query: "SELECT * FROM c WHERE c.type = @type AND c.id = @id",
        parameters: [
          { name: "@type", value: "set" },
          { name: "@id", value: id },
        ],
      })
      .fetchAll();

    return resources[0] ? stripDocumentType(resources[0]) : undefined;
  }

  async listListings(owner?: string): Promise<Listing[]> {
    await this.ensureSeeded();
    const parameters: Array<{ name: string; value: string }> = [{ name: "@type", value: "listing" }];
    let query = "SELECT * FROM c WHERE c.type = @type";

    if (owner) {
      query += " AND c.sellerUserId = @owner";
      parameters.push({ name: "@owner", value: owner });
    }

    query += " ORDER BY c.createdAt DESC";

    const { resources } = await this.container.items
      .query<StoredListing>({ query, parameters })
      .fetchAll();

    return resources.map(stripDocumentType);
  }

  async getListingById(id: string): Promise<Listing | undefined> {
    await this.ensureSeeded();
    const { resources } = await this.container.items
      .query<StoredListing>({
        query: "SELECT * FROM c WHERE c.type = @type AND c.id = @id",
        parameters: [
          { name: "@type", value: "listing" },
          { name: "@id", value: id },
        ],
      })
      .fetchAll();

    return resources[0] ? stripDocumentType(resources[0]) : undefined;
  }

  async createListing(input: ListingMutation, sellerUserId: string): Promise<Listing> {
    await this.ensureSeeded();
    const created: StoredListing = {
      ...input,
      type: "listing",
      id: `listing-${Date.now()}`,
      sellerUserId,
      createdAt: new Date().toISOString(),
    };

    const { resource } = await this.container.items.create(created);
    return stripDocumentType(resource ?? created);
  }

  async updateListing(id: string, input: ListingMutation): Promise<Listing | undefined> {
    await this.ensureSeeded();
    const current = await this.getListingById(id);
    if (!current) {
      return undefined;
    }

    const updated: StoredListing = {
      ...current,
      ...input,
      type: "listing",
    };

    const { resource } = await this.container.item(id, "listing").replace(updated);
    return stripDocumentType(resource ?? updated);
  }

  async deleteListing(id: string): Promise<boolean> {
    await this.ensureSeeded();
    const current = await this.getListingById(id);
    if (!current) {
      return false;
    }

    await this.container.item(id, "listing").delete();
    return true;
  }
}

function stripDocumentType<T extends StoredSet | StoredListing>(item: T): Omit<T, "type"> {
  const { type: _type, ...rest } = item;
  return rest;
}

let storePromise: Promise<DataStore> | undefined;

export function resetDataStoreForTests(): void {
  storePromise = undefined;
}

export function getDataStore(): Promise<DataStore> {
  if (!storePromise) {
    storePromise = createDataStore();
  }

  return storePromise;
}

async function createDataStore(): Promise<DataStore> {
  const connectionString = process.env.COSMOS_CONNECTION_STRING;
  const databaseName = process.env.COSMOS_DATABASE_NAME;
  const containerName = process.env.COSMOS_CONTAINER_NAME;

  if (!connectionString || !databaseName || !containerName) {
    return new MockDataStore();
  }

  const client = new CosmosClient(connectionString);
  const { database } = await client.databases.createIfNotExists({ id: databaseName });
  const { container } = await database.containers.createIfNotExists({
    id: containerName,
    partitionKey: { paths: ["/type"] },
  });

  return new CosmosDataStore(container);
}
