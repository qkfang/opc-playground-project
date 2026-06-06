const defaultBackendBaseUrl = "http://localhost:7071/api";

function getBackendBaseUrl(): string {
  return (process.env.BACKEND_API_BASE_URL ?? defaultBackendBaseUrl).replace(/\/$/, "");
}

function getForwardHeaders(request: Request): HeadersInit {
  const headers = new Headers();
  const contentType = request.headers.get("content-type");
  const principal = request.headers.get("x-ms-client-principal");
  const userId = request.headers.get("x-user-id");

  if (contentType) {
    headers.set("content-type", contentType);
  }
  if (principal) {
    headers.set("x-ms-client-principal", principal);
  }
  if (userId) {
    headers.set("x-user-id", userId);
  }

  return headers;
}

function getResponseHeaders(response: Response): Headers {
  const headers = new Headers();
  const contentType = response.headers.get("content-type");
  const dataSource = response.headers.get("x-data-source");

  if (contentType) {
    headers.set("content-type", contentType);
  }
  if (dataSource) {
    headers.set("x-data-source", dataSource);
  }

  return headers;
}

export async function proxyBackend(request: Request, pathWithQuery: string): Promise<Response> {
  try {
    const method = request.method.toUpperCase();
    const backendResponse = await fetch(`${getBackendBaseUrl()}${pathWithQuery}`, {
      method,
      headers: getForwardHeaders(request),
      body: method === "GET" || method === "HEAD" ? undefined : await request.text(),
      cache: "no-store",
    });

    return new Response(backendResponse.body, {
      status: backendResponse.status,
      headers: getResponseHeaders(backendResponse),
    });
  } catch {
    return new Response(
      JSON.stringify({
        message:
          "Backend API is unavailable. Make sure the Functions backend is running.",
      }),
      {
        status: 503,
        headers: {
          "content-type": "application/json; charset=utf-8",
        },
      }
    );
  }
}
