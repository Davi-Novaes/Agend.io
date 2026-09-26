"use client";

import * as React from "react";
import {
  platformLogin as apiPlatformLogin,
  verifyPlatformMfa as apiVerifyPlatformMfa,
  type PlatformAuthTokens,
} from "@/lib/api/client";

type PlatformSession = {
  accessToken: string;
  expiresAtUtc: string;
  fullName: string;
};

// Lancado por login() quando a senha confirmou mas falta o segundo fator —
// mesmo papel de MfaRequiredError (session-context.tsx) do lado do tenant.
export class PlatformMfaRequiredError extends Error {
  constructor(public readonly mfaChallengeToken: string) {
    super("MFA obrigatorio para concluir o login.");
    this.name = "PlatformMfaRequiredError";
  }
}

type PlatformSessionContextValue = {
  session: PlatformSession | null;
  isAuthenticating: boolean;
  login: (input: { email: string; password: string; turnstileToken: string }) => Promise<void>;
  verifyMfa: (input: { mfaChallengeToken: string; code: string }) => Promise<void>;
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

  const applyTokens = React.useCallback((tokens: PlatformAuthTokens) => {
    setSession({ accessToken: tokens.accessToken, expiresAtUtc: tokens.expiresAtUtc, fullName: tokens.fullName });
  }, []);

  const login = React.useCallback(
    async (input: { email: string; password: string; turnstileToken: string }) => {
      setIsAuthenticating(true);
      try {
        const result = await apiPlatformLogin(input);
        if (result.mfaRequired) {
          throw new PlatformMfaRequiredError(result.mfaChallengeToken);
        }
        applyTokens(result);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [applyTokens]
  );

  const verifyMfa = React.useCallback(
    async (input: { mfaChallengeToken: string; code: string }) => {
      setIsAuthenticating(true);
      try {
        const tokens = await apiVerifyPlatformMfa(input);
        applyTokens(tokens);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [applyTokens]
  );

  const logout = React.useCallback(() => {
    setSession(null);
  }, []);

  const value = React.useMemo(
    () => ({ session, isAuthenticating, login, verifyMfa, logout }),
    [session, isAuthenticating, login, verifyMfa, logout]
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
