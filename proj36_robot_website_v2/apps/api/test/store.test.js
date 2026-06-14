"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const store = require("../src/store");

test("rejects empty payload with field errors", () => {
  store._reset();
  const r = store.validate({});
  assert.equal(r.ok, false);
  assert.ok(r.errors.name);
  assert.ok(r.errors.email);
  assert.ok(r.errors.message);
});

test("rejects invalid email", () => {
  const r = store.validate({ name: "Dan", email: "not-an-email", message: "hi there" });
  assert.equal(r.ok, false);
  assert.equal(r.errors.email, "Please enter a valid email.");
});

test("accepts a valid payload and clamps rating", () => {
  const r = store.validate({ name: "  Dan  ", email: "dan@example.com", rating: "9", message: "  great bots  " });
  assert.equal(r.ok, true);
  assert.equal(r.value.name, "Dan");
  assert.equal(r.value.message, "great bots");
  assert.equal(r.value.rating, 5, "rating clamped to max 5");
});

test("rating below range clamps to 1, missing rating is null", () => {
  assert.equal(store.validate({ name: "A", email: "a@b.co", rating: 0, message: "x" }).value.rating, 1);
  assert.equal(store.validate({ name: "A", email: "a@b.co", message: "x" }).value.rating, null);
});

test("addFeedback persists and listFeedback returns newest first with masked email", () => {
  store._reset();
  store.addFeedback({ name: "First", email: "first@example.com", rating: 4, message: "one" });
  store.addFeedback({ name: "Second", email: "second@example.com", rating: 5, message: "two" });

  assert.equal(store.count(), 2);
  const items = store.listFeedback();
  assert.equal(items.length, 2);
  assert.equal(items[0].name, "Second", "newest first");
  assert.equal(items[1].name, "First");
  // email masked, not raw
  assert.match(items[0].email, /^s\*\*\*@example\.com$/);
  assert.ok(!items[0].email.includes("second@"));
  // has id + ISO timestamp
  assert.match(items[0].id, /^fb_/);
  assert.ok(!Number.isNaN(Date.parse(items[0].createdAt)));
});

test("listFeedback respects limit", () => {
  store._reset();
  for (let i = 0; i < 5; i++) store.addFeedback({ name: "N" + i, email: "n@b.co", message: "m" + i });
  assert.equal(store.listFeedback(2).length, 2);
  assert.equal(store.count(), 5);
});
