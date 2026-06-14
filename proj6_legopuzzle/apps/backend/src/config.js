const DEFAULT_PORT = 3000;
const MAX_PORT = 65535;

function getOptionalString(value) {
  return typeof value === "string" && value.trim() ? value.trim() : undefined;
}

function parsePort(value) {
  if (!value) {
    return DEFAULT_PORT;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0 || parsed > MAX_PORT) {
    throw new Error("PORT must be a valid TCP port number");
  }

  return parsed;
}

function loadConfig(env = process.env) {
  const endpoint = getOptionalString(env.COSMOS_ENDPOINT);
  const key = getOptionalString(env.COSMOS_KEY);
  const databaseId = getOptionalString(env.COSMOS_DATABASE_ID);
  const containerId = getOptionalString(env.COSMOS_CONTAINER_ID);

  const providedCosmosFields = [endpoint, key, databaseId, containerId].filter(Boolean).length;
  if (providedCosmosFields > 0 && providedCosmosFields < 4) {
    throw new Error(
      "Incomplete Cosmos DB configuration. Set COSMOS_ENDPOINT, COSMOS_KEY, COSMOS_DATABASE_ID, and COSMOS_CONTAINER_ID together.",
    );
  }

  return {
    port: parsePort(getOptionalString(env.PORT)),
    cosmos: providedCosmosFields === 4
      ? {
          endpoint,
          key,
          databaseId,
          containerId,
        }
      : undefined,
  };
}

module.exports = {
  loadConfig,
};
