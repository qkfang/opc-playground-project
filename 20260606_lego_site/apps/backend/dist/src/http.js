"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.jsonResponse = jsonResponse;
exports.emptyResponse = emptyResponse;
exports.badRequest = badRequest;
exports.parseListingMutation = parseListingMutation;
function jsonResponse(status, body, dataSource) {
    return new Response(JSON.stringify(body), {
        status,
        headers: {
            "content-type": "application/json; charset=utf-8",
            "x-data-source": dataSource,
        },
    });
}
function emptyResponse(status, dataSource) {
    return new Response(null, {
        status,
        headers: {
            "x-data-source": dataSource,
        },
    });
}
function badRequest(message, dataSource) {
    return jsonResponse(400, { message }, dataSource);
}
function getString(value, fieldName) {
    if (typeof value !== "string" || value.trim().length === 0) {
        throw new Error(`${fieldName} is required`);
    }
    return value.trim();
}
function parseListingMutation(input) {
    if (typeof input !== "object" || input === null) {
        throw new Error("Request body must be a JSON object");
    }
    const record = input;
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
//# sourceMappingURL=http.js.map