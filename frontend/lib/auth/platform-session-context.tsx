"use client";

import * as React from "react";
import { platformLogin as apiPlatformLogin, type PlatformAuthTokens } from "@/lib/api/client";

type PlatformSession = {
  accessToken: string;
  expiresAtUtc: string;
  fullName: string;
};

type PlatformSessionContextValue = {
  session: PlatformSession | null;
  isAuthenticating: boolean;
  login: (input: { email: string; password: string }) => Promise<void>;
  logout: () => void;
};

const PlatformSessionContext = React.createContext<PlatformSessionContextValue | null>(null);

// Contexto TOTALMENTE separado de SessionProvider (painel do estabelecimento):
// o Super Admin e uma autoridade propria, sem refresh token/familia — sessao
// curta (30min) e o re-login e trivial (ver ADR do Sprint 6). Access token so
// em memoria, mesmo raciocinio do painel do tenant.
export function PlatformSessionProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = React.useState<PlatformSession | null>(null);
  const [isAuthenticating, setIsAuthenticating] = React.useState(false);

  const login = React.useCallback(async (input: { email: string; password: string }) => {
    setIsAuthenticating(true);
    try {
      const tokens: PlatformAuthTokens = await apiPlatformLogin(input);
      setSession({ accessToken: tokens.accessToken, expiresAtUtc: tokens.expiresAtUtc, fullName: tokens.fullName });
    } finally {
      setIsAuthenticating(false);
    }
  }, []);

  const logout = React.useCallback(() => {
    setSession(null);
  }, []);

  const value = React.useMemo(
    () => ({ session, isAuthenticating, login, logout }),
    [session, isAuthenticating, login, logout]
  );

  return <PlatformSessionContext.Provider value={value}>{children}</PlatformSessionContext.Provider>;
}

export function usePlatformSession(): PlatformSessionContextValue {
  const context = React.useContext(PlatformSessionContext);
  if (!context) {
    throw new Error("usePlatformSession deve ser usado dentro de um <PlatformSessionProvider>");
  }
  return context;
}
