"use client";

import { useEffect, useState } from "react";
import { fetchCurrentUser, type CurrentUser } from "@/lib/auth";

export default function AuthStatus() {
  const [user, setUser] = useState<CurrentUser | null>(null);

  useEffect(() => {
    fetchCurrentUser().then(setUser).catch(() => setUser(null));
  }, []);

  if (!user) {
    return (
      <a className="hover:text-blue-700" href="/.auth/login/github">
        Sign in
      </a>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <span className="text-slate-600">{user.userDetails ?? user.userId}</span>
      {!user.isLocalDev && (
        <a className="hover:text-blue-700" href="/.auth/logout">
          Sign out
        </a>
      )}
    </div>
  );
}
