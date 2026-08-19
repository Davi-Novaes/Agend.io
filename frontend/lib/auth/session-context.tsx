"use client";

import * as React from "react";
import {
  login as apiLogin,
  verifyMfa as apiVerifyMfa,
  refreshAccessToken,
  logout as apiLogout,
  type AuthTokens,
} from "@/lib/api/client";

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
  isRestoringSession: boolean;
  login: (input: { tenantId: string; email: string; password: string }) => Promise<void>;
  verifyMfa: (input: { mfaChallengeToken: string; code: string }) => Promise<void>;
  logout: () => void;
};

const SessionContext = React.createContext<SessionContextValue | null>(null);

// Renova o access token 1 minuto antes de vencer (vida de 15min, ver
// CLAUDE.md) — a sessao nunca deveria cair por token expirado em uso normal.
const REFRESH_MARGIN_MS = 60_000;

// O access token so existe em memoria (estado React) — nunca em localStorage
// ou cookie legivel por JS. Por isso ao montar (inclui reload de pagina) a
// sessao PRECISA ser reconstruida via /api/auth/refresh usando o cookie
// HttpOnly do refresh token — sem isso todo reload manda pro login mesmo com
// uma sessao valida.
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = React.useState<Session | null>(null);
  const [isAuthenticating, setIsAuthenticating] = React.useState(false);
  const [isRestoringSession, setIsRestoringSession] = React.useState(true);

  const applyTokens = React.useCallback((tokens: AuthTokens) => {
    setSession({ accessToken: tokens.accessToken, expiresAtUtc: tokens.expiresAtUtc });
  }, []);

  React.useEffect(() => {
    let cancelled = false;
    refreshAccessToken()
      .then((tokens) => {
        if (!cancelled) applyTokens(tokens);
      })
      .catch(() => {
        // Sem cookie de refresh valido (nunca logou, expirou, ou foi
        // revogado por logout) — comportamento esperado, fica deslogado.
      })
      .finally(() => {
        if (!cancelled) setIsRestoringSession(false);
      });
    return () => {
      cancelled = true;
    };
  }, [applyTokens]);

  // Agenda a proxima renovacao pouco antes do access token atual vencer, pra
  // sessao ficar viva indefinidamente enquanto o refresh token (30 dias) for
  // valido, sem exigir um interceptor de 401-e-repete em toda chamada de API.
  React.useEffect(() => {
    if (!session) return;

    const msUntilRefresh = Math.max(0, new Date(session.expiresAtUtc).getTime() - Date.now() - REFRESH_MARGIN_MS);
    const timer = setTimeout(() => {
      refreshAccessToken()
        .then(applyTokens)
        .catch(() => setSession(null));
    }, msUntilRefresh);

    return () => clearTimeout(timer);
  }, [session, applyTokens]);

  const login = React.useCallback(
    async (input: { tenantId: string; email: string; password: string }) => {
      setIsAuthenticating(true);
      try {
        const result = await apiLogin(input);
        if (result.mfaRequired) {
          throw new MfaRequiredError(result.mfaChallengeToken);
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
        const tokens = await apiVerifyMfa(input);
        applyTokens(tokens);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [applyTokens]
  );

  const logout = React.useCallback(() => {
    setSession(null);
    // Best-effort: revoga o refresh token no servidor e limpa o cookie. Se
    // falhar (rede fora, etc.), a sessao local ja foi encerrada de qualquer
    // forma — nao ha nada de mais grave a fazer aqui.
    void apiLogout().catch(() => {});
  }, []);

  const value = React.useMemo(
    () => ({ session, isAuthenticating, isRestoringSession, login, verifyMfa, logout }),
    [session, isAuthenticating, isRestoringSession, login, verifyMfa, logout]
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
