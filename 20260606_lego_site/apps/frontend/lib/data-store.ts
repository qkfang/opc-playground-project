import { mockListings, mockSets } from "./mock-data";
import type { LegoSet, Listing, ListingInput } from "./types";

const sets: LegoSet[] = [...mockSets];
const listings: Listing[] = [...mockListings];

export function getSets(): LegoSet[] {
  return sets;
}

export function getSetById(id: string): LegoSet | undefined {
  return sets.find((set) => set.id === id);
}

export function getListings(owner?: string): Listing[] {
  if (!owner) {
    return listings;
  }

  return listings.filter((listing) => listing.sellerUserId === owner);
}

export function getListingById(id: string): Listing | undefined {
  return listings.find((listing) => listing.id === id);
}

export function createListing(input: ListingInput): Listing {
  const newListing: Listing = {
    ...input,
    id: `listing-${Date.now()}`,
    sellerUserId: "demo-user",
    createdAt: new Date().toISOString(),
  };

  listings.unshift(newListing);
  return newListing;
}

export function updateListing(id: string, input: ListingInput): Listing | undefined {
  const index = listings.findIndex((listing) => listing.id === id);
  if (index < 0) {
    return undefined;
  }

  const updated: Listing = {
    ...listings[index],
    ...input,
  };

  listings[index] = updated;
  return updated;
}
