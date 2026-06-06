import type { HttpRequest } from "@azure/functions";

export type AuthenticatedUser = {
  userId: string;
  userDetails?: string;
  identityProvider?: string;
};

type StaticWebAppPrincipal = {
  userId?: string;
  userDetails?: string;
  identityProvider?: string;
};

export function getAuthenticatedUser(request: HttpRequest): AuthenticatedUser | undefined {
  if (request.user?.id) {
    return {
      userId: request.user.id,
      userDetails: request.user.username,
      identityProvider: request.user.identityProvider,
    };
  }

  const principalHeader = request.headers.get("x-ms-client-principal");
  if (principalHeader) {
    try {
      const principal = JSON.parse(Buffer.from(principalHeader, "base64").toString("utf8")) as StaticWebAppPrincipal;
      if (principal.userId) {
        return {
          userId: principal.userId,
          userDetails: principal.userDetails,
          identityProvider: principal.identityProvider,
        };
      }
    } catch {
      // Ignore malformed client principal headers and fall through to the next auth source.
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
