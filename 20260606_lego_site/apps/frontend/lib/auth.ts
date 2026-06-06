export type CurrentUser = {
  userId: string;
  userDetails?: string;
  identityProvider?: string;
  isLocalDev?: boolean;
};

type StaticWebAppPrincipal = {
  userId?: string;
  userDetails?: string;
  identityProvider?: string;
};

type StaticWebAppAuthResponse = Array<{
  clientPrincipal?: StaticWebAppPrincipal;
}>;

export async function fetchCurrentUser(): Promise<CurrentUser | null> {
  try {
    const response = await fetch("/.auth/me", { cache: "no-store" });
    if (response.ok) {
      const data = (await response.json()) as StaticWebAppAuthResponse;
      const principal = data[0]?.clientPrincipal;
      if (principal?.userId) {
        return {
          userId: principal.userId,
          userDetails: principal.userDetails,
          identityProvider: principal.identityProvider,
        };
      }
    }
  } catch {
    // Ignore auth endpoint errors and fall back to local development auth.
  }

  const localDevUserId = process.env.NEXT_PUBLIC_LOCAL_USER_ID?.trim();
  if (localDevUserId) {
    return {
      userId: localDevUserId,
      userDetails: localDevUserId,
      identityProvider: "local-dev",
      isLocalDev: true,
    };
  }

  return null;
}
