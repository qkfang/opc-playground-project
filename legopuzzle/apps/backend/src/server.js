const { loadConfig } = require("./config");
const { createScoreStore } = require("./score-store");
const { createApp } = require("./app");

async function startServer() {
  const config = loadConfig();
  const scoreStore = await createScoreStore(config);
  const app = createApp(scoreStore);

  app.listen(config.port, () => {
    console.log(`Backend listening on port ${config.port}`);
  });
}

startServer().catch((error) => {
  console.error(error);
  process.exit(1);
});
