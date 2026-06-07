function toBoundedInteger(value, field, { min, max }) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < min || parsed > max) {
    throw new Error(`${field} must be an integer between ${min} and ${max}`);
  }

  return parsed;
}

function toPlayerName(value) {
  if (typeof value !== "string") {
    throw new Error("playerName is required");
  }

  const trimmed = value.trim();
  if (!trimmed) {
    throw new Error("playerName is required");
  }

  if (trimmed.length > 40) {
    throw new Error("playerName must be 40 characters or fewer");
  }

  return trimmed;
}

function parseScorePayload(body) {
  if (!body || typeof body !== "object" || Array.isArray(body)) {
    throw new Error("Request body must be a JSON object");
  }

  return {
    playerName: toPlayerName(body.playerName),
    score: toBoundedInteger(body.score, "score", { min: 0, max: 1000000000 }),
    completionTimeMs: toBoundedInteger(body.completionTimeMs, "completionTimeMs", { min: 1, max: 86400000 }),
  };
}

function parseLeaderboardLimit(value) {
  if (value === undefined) {
    return 10;
  }

  return toBoundedInteger(value, "limit", { min: 1, max: 100 });
}

module.exports = {
  parseScorePayload,
  parseLeaderboardLimit,
};
