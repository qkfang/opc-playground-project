import type { Listing, LegoSet, StoredListing, StoredSet } from "./types";

export const defaultSets: LegoSet[] = [
  {
    id: "10326",
    name: "Natural History Museum",
    theme: "Icons",
    year: 2023,
    pieces: 4014,
    imageUrl: "https://images.lego.com/is/image/lego/10326",
  },
  {
    id: "21345",
    name: "Polaroid OneStep SX-70 Camera",
    theme: "Ideas",
    year: 2024,
    pieces: 516,
    imageUrl: "https://images.lego.com/is/image/lego/21345",
  },
  {
    id: "42154",
    name: "Ford GT 2022",
    theme: "Technic",
    year: 2023,
    pieces: 1466,
    imageUrl: "https://images.lego.com/is/image/lego/42154",
  },
];

export const defaultListings: Listing[] = [
  {
    id: "listing-1",
    setId: "10326",
    title: "NISB Natural History Museum",
    condition: "new",
    price: 289.99,
    currency: "USD",
    description: "Factory sealed box, corner wear only.",
    sellerUserId: "demo-user",
    createdAt: "2026-06-01T10:00:00.000Z",
    status: "active",
  },
  {
    id: "listing-2",
    setId: "42154",
    title: "Built once, complete with manual",
    condition: "used-like-new",
    price: 79.5,
    currency: "USD",
    description: "Displayed in smoke-free home.",
    sellerUserId: "brickfan88",
    createdAt: "2026-06-02T13:40:00.000Z",
    status: "active",
  },
];

export function getSeedSetDocuments(): StoredSet[] {
  return defaultSets.map((set) => ({ ...set, type: "set" }));
}

export function getSeedListingDocuments(): StoredListing[] {
  return defaultListings.map((listing) => ({ ...listing, type: "listing" }));
}
