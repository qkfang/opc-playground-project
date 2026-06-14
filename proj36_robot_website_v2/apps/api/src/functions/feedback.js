"use strict";

/**
 * HTTP API for Cogsworth Robotics 2.0 feedback.
 *
 *   POST /api/feedback   -> validate + save to in-memory store, returns the created entry
 *   GET  /api/feedback   -> list stored feedback (newest first, emails masked) + count
 *   GET  /api/health     -> liveness + current store size
 *
 * Node.js v4 programming model (@azure/functions v4). Anonymous auth so the
 * Static Web App front-end can call it directly under /api/*.
 */

const { app } = require("@azure/functions");
const store = require("../store");

const JSON_HEADERS = { "Content-Type": "application/json" };

/** Read + parse the JSON body defensively (empty / malformed bodies are handled). */
async function readJson(request) {
  try {
    const text = await request.text();
    if (!text) return {};
    return JSON.parse(text);
  } catch (err) {
    return undefined; // signal "unparseable"
  }
}

async function postFeedback(request, context) {
  const payload = await readJson(request);
  if (payload === undefined) {
    return {
      status: 400,
      headers: JSON_HEADERS,
      jsonBody: { ok: false, error: "Request body must be valid JSON." },
    };
  }

  const result = store.validate(payload);
  if (!result.ok) {
    return {
      status: 400,
      headers: JSON_HEADERS,
      jsonBody: { ok: false, error: "Validation failed.", fields: result.errors },
    };
  }

  const entry = store.addFeedback(result.value);
  context.log(
    `feedback saved id=${entry.id} rating=${entry.rating === null ? "-" : entry.rating} total=${store.count()}`
  );

  return {
    status: 201,
    headers: JSON_HEADERS,
    jsonBody: {
      ok: true,
      message: "Thanks for the feedback!",
      entry: store.toPublic(entry),
      total: store.count(),
    },
  };
}

async function getFeedback(request, context) {
  const limitRaw = request.query.get("limit");
  let limit = undefined;
  if (limitRaw) {
    const n = parseInt(limitRaw, 10);
    if (Number.isFinite(n) && n > 0) limit = Math.min(n, 200);
  }

  const items = store.listFeedback(limit);
  return {
    status: 200,
    headers: JSON_HEADERS,
    jsonBody: { ok: true, count: store.count(), items: items },
  };
}

async function getHealth() {
  return {
    status: 200,
    headers: JSON_HEADERS,
    jsonBody: { ok: true, service: "cogsworth-feedback-api", store: "in-memory", count: store.count() },
  };
}

app.http("feedbackPost", {
  route: "feedback",
  methods: ["POST"],
  authLevel: "anonymous",
  handler: postFeedback,
});

app.http("feedbackList", {
  route: "feedback",
  methods: ["GET"],
  authLevel: "anonymous",
  handler: getFeedback,
});

app.http("health", {
  route: "health",
  methods: ["GET"],
  authLevel: "anonymous",
  handler: getHealth,
});

module.exports = { postFeedback, getFeedback, getHealth };
