// Dev-only proxy: serves the static front-end from apps/web AND forwards
// /api/* to the local Azure Functions host (port 7071), mirroring how Azure
// Static Web Apps routes managed Functions. Used only for local end-to-end
// browser verification of the feedback form -> API integration.
//
//   node temp/dev-proxy.mjs   ->  http://localhost:4180
//
import http from "node:http";
import { readFile, stat } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = normalize(join(__dirname, "..", "apps", "web"));
const API_HOST = "127.0.0.1";
const API_PORT = 7071;
const PORT = 4180;

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".svg": "image/svg+xml",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".ico": "image/x-icon",
  ".txt": "text/plain; charset=utf-8",
};

function proxyApi(req, res) {
  const chunks = [];
  req.on("data", (c) => chunks.push(c));
  req.on("end", () => {
    const body = Buffer.concat(chunks);
    const headers = { ...req.headers, host: `${API_HOST}:${API_PORT}` };
    const upstream = http.request(
      { host: API_HOST, port: API_PORT, method: req.method, path: req.url, headers },
      (up) => {
        res.writeHead(up.statusCode || 502, up.headers);
        up.pipe(res);
      }
    );
    upstream.on("error", (e) => {
      res.writeHead(502, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: false, error: "api proxy error: " + e.message }));
    });
    if (body.length) upstream.write(body);
    upstream.end();
  });
}

async function serveStatic(req, res) {
  let urlPath = decodeURIComponent((req.url || "/").split("?")[0]);
  if (urlPath === "/" || urlPath === "") urlPath = "/index.html";
  let filePath = normalize(join(WEB_ROOT, urlPath));
  if (!filePath.startsWith(WEB_ROOT)) {
    res.writeHead(403);
    res.end("forbidden");
    return;
  }
  try {
    const s = await stat(filePath);
    if (s.isDirectory()) filePath = join(filePath, "index.html");
  } catch {
    // SWA-style fallback to index.html for unknown non-asset routes
    filePath = join(WEB_ROOT, "index.html");
  }
  try {
    const data = await readFile(filePath);
    res.writeHead(200, { "Content-Type": MIME[extname(filePath)] || "application/octet-stream" });
    res.end(data);
  } catch {
    res.writeHead(404);
    res.end("not found");
  }
}

http
  .createServer((req, res) => {
    if ((req.url || "").startsWith("/api/")) return proxyApi(req, res);
    return serveStatic(req, res);
  })
  .listen(PORT, () => {
    console.log(`dev-proxy: http://localhost:${PORT}  (static: ${WEB_ROOT}, /api -> :${API_PORT})`);
  });
