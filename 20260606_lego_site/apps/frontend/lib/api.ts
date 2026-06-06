import type { LegoSet, Listing, ListingInput } from "./types";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...init?.headers,
    },
  });

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`);
  }

  return (await response.json()) as T;
}

export function fetchSets(): Promise<LegoSet[]> {
  return request<LegoSet[]>("/api/sets");
}

export function fetchSet(id: string): Promise<LegoSet> {
  return request<LegoSet>(`/api/sets/${id}`);
}

export function fetchListings(owner?: string): Promise<Listing[]> {
  const url = owner
    ? `/api/listings?owner=${encodeURIComponent(owner)}`
    : "/api/listings";
  return request<Listing[]>(url);
}

export function fetchListing(id: string): Promise<Listing> {
  return request<Listing>(`/api/listings/${id}`);
}

export function createListing(input: ListingInput): Promise<Listing> {
  return request<Listing>("/api/listings", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateListing(id: string, input: ListingInput): Promise<Listing> {
  return request<Listing>(`/api/listings/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}
