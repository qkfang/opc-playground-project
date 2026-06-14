// Minimal zero-dependency static file server for local verification.
// Usage: node scripts/serve.mjs [port]   (default 4175), serves apps/web
import http from "node:http";
import { readFile, stat } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, "..", "apps", "web");
const port = parseInt(process.argv[2] || "4175", 10);

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".ico": "image/x-icon",
};

const server = http.createServer(async (req, res) => {
  try {
    let urlPath = decodeURIComponent((req.url || "/").split("?")[0]);
    if (urlPath === "/") urlPath = "/index.html";
    const filePath = normalize(join(ROOT, urlPath));
    if (!filePath.startsWith(ROOT)) { res.writeHead(403); return res.end("Forbidden"); }
    try {
      const s = await stat(filePath);
      if (s.isDirectory()) { res.writeHead(301, { Location: urlPath + "/index.html" }); return res.end(); }
    } catch {
      res.writeHead(404, { "Content-Type": "text/html" });
      return res.end("<h1>404</h1>");
    }
    const data = await readFile(filePath);
    res.writeHead(200, { "Content-Type": MIME[extname(filePath)] || "application/octet-stream" });
    res.end(data);
  } catch (e) {
    res.writeHead(500);
    res.end("Server error: " + e.message);
  }
});

server.listen(port, () => {
  console.log(`Cogsworth Robotics dev server: http://127.0.0.1:${port}/`);
});
