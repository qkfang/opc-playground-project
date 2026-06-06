"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.getAuthenticatedUser = getAuthenticatedUser;
function getAuthenticatedUser(request) {
    if (request.user?.id) {
        return {
            userId: request.user.id,
            userDetails: request.user.username,
            identityProvider: request.user.identityProvider,
        };
    }
    const principalHeader = request.headers.get("x-ms-client-principal");
    if (principalHeader) {
        const principal = JSON.parse(Buffer.from(principalHeader, "base64").toString("utf8"));
        if (principal.userId) {
            return {
                userId: principal.userId,
                userDetails: principal.userDetails,
                identityProvider: principal.identityProvider,
            };
        }
    }
    if (process.env.ALLOW_LOCAL_DEV_AUTH === "true") {
        const localUserId = request.headers.get("x-user-id");
        if (localUserId) {
            return {
                userId: localUserId,
                identityProvider: "local-dev",
            };
        }
    }
    return undefined;
}
//# sourceMappingURL=auth.js.map