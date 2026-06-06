"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.getSets = getSets;
exports.getSetById = getSetById;
const functions_1 = require("@azure/functions");
const data_store_1 = require("../data-store");
const http_1 = require("../http");
async function getSets(request, _context) {
    const store = await (0, data_store_1.getDataStore)();
    return (0, http_1.jsonResponse)(200, await store.listSets(), store.dataSource);
}
async function getSetById(request, _context) {
    const store = await (0, data_store_1.getDataStore)();
    const set = await store.getSetById(request.params.id);
    if (!set) {
        return (0, http_1.jsonResponse)(404, { message: "Set not found" }, store.dataSource);
    }
    return (0, http_1.jsonResponse)(200, set, store.dataSource);
}
functions_1.app.http("sets-list", {
    route: "sets",
    methods: ["GET"],
    authLevel: "anonymous",
    handler: getSets,
});
functions_1.app.http("sets-get", {
    route: "sets/{id}",
    methods: ["GET"],
    authLevel: "anonymous",
    handler: getSetById,
});
//# sourceMappingURL=sets.js.map