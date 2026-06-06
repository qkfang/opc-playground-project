import type { LegoSet, Listing, ListingInput } from "./types";

const localDevUserId = process.env.NEXT_PUBLIC_LOCAL_USER_ID?.trim();

// When deployed to SWA (static export), the backend Azure Functions app lives on a
// different origin, so prefix API calls with its base URL. Empty => same-origin
// relative paths (local dev).
const apiBase = (process.env.NEXT_PUBLIC_API_BASE_URL ?? "").replace(/\/$/, "");

function apiUrl(path: string): string {
  return apiBase ? `${apiBase}${path}` : path;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(apiUrl(path), {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(localDevUserId ? { "x-user-id": localDevUserId } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    let message = `Request failed: ${response.status}`;
    try {
      const body = (await response.json()) as { message?: string };
      if (body.message) {
        message = body.message;
      }
    } catch {
      // Ignore invalid/non-JSON responses and keep fallback error message.
    }
    throw new ApiError(response.status, message);
  }

  if (response.status === 204) {
    return undefined as T;
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

export function deleteListing(id: string): Promise<void> {
  return request<void>(`/api/listings/${id}`, {
    method: "DELETE",
  });
}
