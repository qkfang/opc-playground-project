const express = require("express");
const { parseLeaderboardLimit, parseScorePayload } = require("./validation");
const { ValidationError } = require("./errors");

const JSON_BODY_LIMIT = "100kb";

function createApp(scoreStore) {
  const app = express();
  app.disable("x-powered-by");
  app.use(express.json({ limit: JSON_BODY_LIMIT }));

  app.get("/api/health", (_req, res) => {
    res.status(200).json({ status: "ok" });
  });

  app.get("/api/leaderboard", async (req, res, next) => {
    try {
      const limit = parseLeaderboardLimit(req.query.limit);
      const leaderboard = await scoreStore.getLeaderboard(limit);
      res.status(200).json({ items: leaderboard });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/scores", async (req, res, next) => {
    try {
      const payload = parseScorePayload(req.body);
      const saved = await scoreStore.saveScore(payload);
      res.status(201).json(saved);
    } catch (error) {
      next(error);
    }
  });

  app.use((error, _req, res, _next) => {
    if (error instanceof SyntaxError && "status" in error && error.status === 400) {
      return res.status(400).json({ message: "Request body must be valid JSON" });
    }

    if (error instanceof ValidationError) {
      return res.status(400).json({ message: error.message });
    }

    console.error(error);
    return res.status(500).json({ message: "Internal server error" });
  });

  return app;
}

module.exports = {
  createApp,
};
