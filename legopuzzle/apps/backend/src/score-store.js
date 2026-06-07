const { randomUUID } = require("node:crypto");
const { CosmosClient } = require("@azure/cosmos");

class InMemoryScoreStore {
  constructor() {
    this.scores = [];
  }

  async saveScore(input) {
    const score = {
      id: randomUUID(),
      playerName: input.playerName,
      score: input.score,
      completionTimeMs: input.completionTimeMs,
      createdAt: new Date().toISOString(),
    };

    this.scores.push(score);
    return score;
  }

  async getLeaderboard(limit) {
    return [...this.scores]
      .sort(
        (a, b) =>
          b.score - a.score ||
          a.completionTimeMs - b.completionTimeMs ||
          Date.parse(a.createdAt) - Date.parse(b.createdAt),
      )
      .slice(0, limit);
  }
}

class CosmosScoreStore {
  constructor(container) {
    this.container = container;
  }

  async saveScore(input) {
    const score = {
      id: randomUUID(),
      type: "score",
      playerName: input.playerName,
      score: input.score,
      completionTimeMs: input.completionTimeMs,
      createdAt: new Date().toISOString(),
      partitionKey: "score",
    };

    const { resource } = await this.container.items.create(score);
    return {
      id: resource.id,
      playerName: resource.playerName,
      score: resource.score,
      completionTimeMs: resource.completionTimeMs,
      createdAt: resource.createdAt,
    };
  }

  async getLeaderboard(limit) {
    const query = {
      query: `SELECT c.id, c.playerName, c.score, c.completionTimeMs, c.createdAt
              FROM c
              WHERE c.type = @type
              ORDER BY c.score DESC, c.completionTimeMs ASC, c.createdAt ASC
              OFFSET 0 LIMIT @limit`,
      parameters: [
        { name: "@type", value: "score" },
        { name: "@limit", value: limit },
      ],
    };

    const { resources } = await this.container.items.query(query).fetchAll();
    return resources;
  }
}

async function createCosmosScoreStore(cosmosConfig) {
  const client = new CosmosClient({ endpoint: cosmosConfig.endpoint, key: cosmosConfig.key });
  const { database } = await client.databases.createIfNotExists({ id: cosmosConfig.databaseId });
  const { container } = await database.containers.createIfNotExists({
    id: cosmosConfig.containerId,
    partitionKey: { paths: ["/partitionKey"] },
  });

  return new CosmosScoreStore(container);
}

async function createScoreStore(config) {
  if (!config.cosmos) {
    return new InMemoryScoreStore();
  }

  return createCosmosScoreStore(config.cosmos);
}

module.exports = {
  createScoreStore,
  InMemoryScoreStore,
};
