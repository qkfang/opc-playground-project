const { ValidationError } = require("./errors");

const MAX_PLAYER_NAME_LENGTH = 40;
const MAX_SCORE = 1000000000;
const MAX_COMPLETION_TIME_MS = 86400000;
const MAX_LEADERBOARD_LIMIT = 100;

function toBoundedInteger(value, field, { min, max }) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < min || parsed > max) {
    throw new ValidationError(`${field} must be an integer between ${min} and ${max}`);
  }

  return parsed;
}

function toPlayerName(value) {
  if (typeof value !== "string") {
    throw new ValidationError("playerName is required");
  }

  const trimmed = value.trim();
  if (!trimmed) {
    throw new ValidationError("playerName is required");
  }

  if (trimmed.length > MAX_PLAYER_NAME_LENGTH) {
    throw new ValidationError(`playerName must be ${MAX_PLAYER_NAME_LENGTH} characters or fewer`);
  }

  return trimmed;
}

function parseScorePayload(body) {
  if (!body || typeof body !== "object" || Array.isArray(body)) {
    throw new ValidationError("Request body must be a JSON object");
  }

  return {
    playerName: toPlayerName(body.playerName),
    score: toBoundedInteger(body.score, "score", { min: 0, max: MAX_SCORE }),
    completionTimeMs: toBoundedInteger(body.completionTimeMs, "completionTimeMs", { min: 1, max: MAX_COMPLETION_TIME_MS }),
  };
}

function parseLeaderboardLimit(value) {
  if (value === undefined) {
    return 10;
  }

  return toBoundedInteger(value, "limit", { min: 1, max: MAX_LEADERBOARD_LIMIT });
}

module.exports = {
  parseScorePayload,
  parseLeaderboardLimit,
};
