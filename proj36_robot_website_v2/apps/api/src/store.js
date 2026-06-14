"use strict";

/**
 * In-memory feedback store for Cogsworth Robotics 2.0.
 *
 * This is a deliberately simple, process-local "database": feedback entries
 * live in a module-level array for the lifetime of the Functions host process.
 * No external service, no disk, no key — restarting the host clears it.
 *
 * Exposed as a tiny repository so the HTTP functions stay thin and the store
 * is easy to unit-test in isolation.
 */

/** @typedef {{ id: string, name: string, email: string, rating: number|null, message: string, createdAt: string }} FeedbackEntry */

/** @type {FeedbackEntry[]} */
const feedbackEntries = [];

let seq = 0;

/** Generate a short, collision-resistant-enough id without external deps. */
function nextId() {
  seq += 1;
  return (
    "fb_" +
    Date.now().toString(36) +
    "_" +
    seq.toString(36) +
    Math.random().toString(36).slice(2, 6)
  );
}

function clampRating(value) {
  if (value === undefined || value === null || value === "") return null;
  const n = Number(value);
  if (!Number.isFinite(n)) return null;
  const rounded = Math.round(n);
  if (rounded < 1) return 1;
  if (rounded > 5) return 5;
  return rounded;
}

/**
 * Validate and normalise an incoming payload.
 * @returns {{ ok: true, value: { name: string, email: string, rating: number|null, message: string } } | { ok: false, errors: Record<string,string> }}
 */
function validate(payload) {
  const errors = {};
  const data = payload && typeof payload === "object" ? payload : {};

  const name = typeof data.name === "string" ? data.name.trim() : "";
  const email = typeof data.email === "string" ? data.email.trim() : "";
  const message = typeof data.message === "string" ? data.message.trim() : "";
  const rating = clampRating(data.rating);

  if (name.length < 1) errors.name = "Name is required.";
  if (name.length > 120) errors.name = "Name is too long (max 120).";

  // Pragmatic email check — good enough for a demo form.
  if (email.length < 1) {
    errors.email = "Email is required.";
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 200) {
    errors.email = "Please enter a valid email.";
  }

  if (message.length < 1) {
    errors.message = "Feedback message is required.";
  } else if (message.length > 2000) {
    errors.message = "Feedback is too long (max 2000 characters).";
  }

  if (Object.keys(errors).length > 0) return { ok: false, errors: errors };
  return { ok: true, value: { name: name, email: email, rating: rating, message: message } };
}

/**
 * Persist a validated feedback entry to the in-memory store.
 * @returns {FeedbackEntry}
 */
function addFeedback(value) {
  /** @type {FeedbackEntry} */
  const entry = {
    id: nextId(),
    name: value.name,
    email: value.email,
    rating: value.rating,
    message: value.message,
    createdAt: new Date().toISOString(),
  };
  feedbackEntries.push(entry);
  return entry;
}

/** Public-safe view of an entry (email masked so a GET doesn't leak addresses). */
function toPublic(entry) {
  return {
    id: entry.id,
    name: entry.name,
    email: maskEmail(entry.email),
    rating: entry.rating,
    message: entry.message,
    createdAt: entry.createdAt,
  };
}

function maskEmail(email) {
  const at = email.indexOf("@");
  if (at <= 1) return "***" + email.slice(at);
  return email[0] + "***" + email.slice(at);
}

/**
 * List stored feedback, newest first.
 * @param {number} [limit]
 */
function listFeedback(limit) {
  const items = feedbackEntries.slice().reverse();
  const capped = typeof limit === "number" && limit > 0 ? items.slice(0, limit) : items;
  return capped.map(toPublic);
}

function count() {
  return feedbackEntries.length;
}

/** Test-only: wipe the store. */
function _reset() {
  feedbackEntries.length = 0;
  seq = 0;
}

module.exports = {
  validate: validate,
  addFeedback: addFeedback,
  listFeedback: listFeedback,
  count: count,
  toPublic: toPublic,
  _reset: _reset,
};
