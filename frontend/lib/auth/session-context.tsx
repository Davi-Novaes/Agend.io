"use client";

import * as React from "react";
import { login as apiLogin, type AuthTokens } from "@/lib/api/client";

type Session = {
  accessToken: string;
  expiresAtUtc: string;
};

type SessionContextValue = {
  session: Session | null;
  isAuthenticating: boolean;
  login: (input: { tenantId: string; email: string; password: string }) => Promise<void>;
  logout: () => void;
};

const SessionContext = React.createContext<SessionContextValue | null>(null);

// O access token so existe em memoria (estado React) — nunca em localStorage
// ou cookie legivel por JS. Recarregar a pagina exige um novo /api/auth/refresh
// (cookie HttpOnly do refresh token), nao uma leitura de storage local.
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = React.useState<Session | null>(null);
  const [isAuthenticating, setIsAuthenticating] = React.useState(false);

  const login = React.useCallback(
    async (input: { tenantId: string; email: string; password: string }) => {
      setIsAuthenticating(true);
      try {
        const tokens: AuthTokens = await apiLogin(input);
        setSession({ accessToken: tokens.accessToken, expiresAtUtc: tokens.expiresAtUtc });
      } finally {
        setIsAuthenticating(false);
      }
    },
    []
  );

  const logout = React.useCallback(() => {
    setSession(null);
  }, []);

  const value = React.useMemo(
    () => ({ session, isAuthenticating, login, logout }),
    [session, isAuthenticating, login, logout]
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const context = React.useContext(SessionContext);
  if (!context) {
    throw new Error("useSession deve ser usado dentro de um <SessionProvider>");
  }
  return context;
}
