const test = require("node:test");
const assert = require("node:assert/strict");
const request = require("supertest");
const { createApp } = require("../src/app");
const { InMemoryScoreStore } = require("../src/score-store");

function createTestClient() {
  const store = new InMemoryScoreStore();
  return request(createApp(store));
}

test("GET /api/health returns ok", async () => {
  const response = await createTestClient().get("/api/health");
  assert.equal(response.status, 200);
  assert.deepEqual(response.body, { status: "ok" });
});

test("POST /api/scores validates payload", async () => {
  const response = await createTestClient().post("/api/scores").send({ playerName: "", score: -1, completionTimeMs: 0 });
  assert.equal(response.status, 400);
});

test("leaderboard sorts by score desc then completionTime asc and respects limit", async () => {
  const client = createTestClient();

  await client.post("/api/scores").send({ playerName: "A", score: 50, completionTimeMs: 1800 }).expect(201);
  await client.post("/api/scores").send({ playerName: "B", score: 100, completionTimeMs: 5000 }).expect(201);
  await client.post("/api/scores").send({ playerName: "C", score: 100, completionTimeMs: 2500 }).expect(201);

  const response = await client.get("/api/leaderboard?limit=2");

  assert.equal(response.status, 200);
  assert.equal(response.body.items.length, 2);
  assert.equal(response.body.items[0].playerName, "C");
  assert.equal(response.body.items[1].playerName, "B");
});
