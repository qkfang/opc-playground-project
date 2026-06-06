import type { DataSource, ListingMutation } from "./types";

export function jsonResponse(status: number, body: unknown, dataSource: DataSource): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "x-data-source": dataSource,
    },
  });
}

export function emptyResponse(status: number, dataSource: DataSource): Response {
  return new Response(null, {
    status,
    headers: {
      "x-data-source": dataSource,
    },
  });
}

export function badRequest(message: string, dataSource: DataSource): Response {
  return jsonResponse(400, { message }, dataSource);
}

function getString(value: unknown, fieldName: string): string {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error(`${fieldName} is required`);
  }

  return value.trim();
}

export function parseListingMutation(input: unknown): ListingMutation {
  if (typeof input !== "object" || input === null) {
    throw new Error("Request body must be a JSON object");
  }

  const record = input as Record<string, unknown>;
  const price = Number(record.price);
  const status = record.status === "sold" ? "sold" : record.status === "active" || record.status === undefined ? "active" : undefined;

  if (!Number.isFinite(price) || price < 0) {
    throw new Error("price must be a non-negative number");
  }

  if (!status) {
    throw new Error("status must be either 'active' or 'sold'");
  }

  return {
    setId: getString(record.setId, "setId"),
    title: getString(record.title, "title"),
    condition: getString(record.condition, "condition"),
    price,
    currency: getString(record.currency, "currency").toUpperCase(),
    description: getString(record.description, "description"),
    status,
  };
}
