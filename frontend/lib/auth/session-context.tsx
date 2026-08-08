"use client";

import * as React from "react";
import { login as apiLogin, verifyMfa as apiVerifyMfa, type AuthTokens } from "@/lib/api/client";

type Session = {
  accessToken: string;
  expiresAtUtc: string;
};

// Lancado por login() quando a senha confirmou mas falta o segundo fator — a
// tela de login captura isto e troca para a etapa de codigo, em vez de tratar
// como falha de autenticacao.
export class MfaRequiredError extends Error {
  constructor(public readonly mfaChallengeToken: string) {
    super("MFA obrigatorio para concluir o login.");
    this.name = "MfaRequiredError";
  }
}

type SessionContextValue = {
  session: Session | null;
  isAuthenticating: boolean;
  login: (input: { tenantId: string; email: string; password: string }) => Promise<void>;
  verifyMfa: (input: { mfaChallengeToken: string; code: string }) => Promise<void>;
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
        const result = await apiLogin(input);
        if (result.mfaRequired) {
          throw new MfaRequiredError(result.mfaChallengeToken);
        }
        setSession({ accessToken: result.accessToken, expiresAtUtc: result.expiresAtUtc });
      } finally {
        setIsAuthenticating(false);
      }
    },
    []
  );

  const verifyMfa = React.useCallback(async (input: { mfaChallengeToken: string; code: string }) => {
    setIsAuthenticating(true);
    try {
      const tokens: AuthTokens = await apiVerifyMfa(input);
      setSession({ accessToken: tokens.accessToken, expiresAtUtc: tokens.expiresAtUtc });
    } finally {
      setIsAuthenticating(false);
    }
  }, []);

  const logout = React.useCallback(() => {
    setSession(null);
  }, []);

  const value = React.useMemo(
    () => ({ session, isAuthenticating, login, verifyMfa, logout }),
    [session, isAuthenticating, login, verifyMfa, logout]
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
