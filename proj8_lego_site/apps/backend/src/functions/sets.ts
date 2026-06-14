import { app, type HttpRequest, type InvocationContext } from "@azure/functions";
import { getDataStore } from "../data-store";
import { jsonResponse } from "../http";

export async function getSets(request: HttpRequest, _context: InvocationContext): Promise<Response> {
  const store = await getDataStore();
  return jsonResponse(200, await store.listSets(), store.dataSource);
}

export async function getSetById(request: HttpRequest, _context: InvocationContext): Promise<Response> {
  const store = await getDataStore();
  const set = await store.getSetById(request.params.id);

  if (!set) {
    return jsonResponse(404, { message: "Set not found" }, store.dataSource);
  }

  return jsonResponse(200, set, store.dataSource);
}

app.http("sets-list", {
  route: "sets",
  methods: ["GET"],
  authLevel: "anonymous",
  handler: getSets,
});

app.http("sets-get", {
  route: "sets/{id}",
  methods: ["GET"],
  authLevel: "anonymous",
  handler: getSetById,
});
