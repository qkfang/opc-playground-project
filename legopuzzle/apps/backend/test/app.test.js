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
  const response = await createTestClient().post("/api/scores").send({ playerName: "", score: 100, completionTimeMs: 2000 });
  assert.equal(response.status, 400);
  assert.equal(response.body.message, "playerName is required");
});

test("POST /api/scores validates score and completionTimeMs boundaries", async () => {
  const client = createTestClient();

  const badScoreResponse = await client
    .post("/api/scores")
    .send({ playerName: "Demo", score: -1, completionTimeMs: 2000 });
  assert.equal(badScoreResponse.status, 400);
  assert.equal(badScoreResponse.body.message, "score must be an integer between 0 and 1000000000");

  const badCompletionResponse = await client
    .post("/api/scores")
    .send({ playerName: "Demo", score: 100, completionTimeMs: 0 });
  assert.equal(badCompletionResponse.status, 400);
  assert.equal(badCompletionResponse.body.message, "completionTimeMs must be an integer between 1 and 86400000");
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
