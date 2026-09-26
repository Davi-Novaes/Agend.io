"use client";

import * as React from "react";

type TurnstileRenderOptions = {
  sitekey: string;
  callback: (token: string) => void;
  "expired-callback"?: () => void;
  "error-callback"?: () => void;
  theme?: "light" | "dark" | "auto";
};

declare global {
  interface Window {
    turnstile?: {
      render: (container: HTMLElement, options: TurnstileRenderOptions) => string;
      remove: (widgetId: string) => void;
    };
  }
}

const TURNSTILE_SCRIPT_SRC = "https://challenges.cloudflare.com/turnstile/v0/api.js";

// Chave de teste publica da Cloudflare (documentada, sempre aprova) -- usada
// quando NEXT_PUBLIC_TURNSTILE_SITE_KEY nao esta configurada: dev local, ou
// producao antes da chave real da conta Cloudflare estar pronta (ver
// docker-compose.prod.yml/Turnstile__SecretKey no backend, mesmo raciocinio).
const FALLBACK_SITE_KEY = "1x00000000000000000000AA";

let scriptLoadPromise: Promise<void> | null = null;

function loadTurnstileScript(): Promise<void> {
  if (window.turnstile) {
    return Promise.resolve();
  }

  scriptLoadPromise ??= new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>(`script[src="${TURNSTILE_SCRIPT_SRC}"]`);
    if (existing) {
      existing.addEventListener("load", () => resolve());
      existing.addEventListener("error", () => reject(new Error("Falha ao carregar o Turnstile.")));
      return;
    }

    const script = document.createElement("script");
    script.src = TURNSTILE_SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Falha ao carregar o Turnstile."));
    document.head.appendChild(script);
  });

  return scriptLoadPromise;
}

/**
 * Widget Cloudflare Turnstile ("prove que voce e humano" sem CAPTCHA
 * tradicional) -- usa a API JS explicita (render/remove) em vez do
 * carregamento implicito por data-attribute, pra funcionar bem com
 * mount/unmount do React (troca de etapa do formulario, StrictMode).
 */
export function TurnstileWidget({ onVerify, onExpire }: { onVerify: (token: string) => void; onExpire?: () => void }) {
  const containerRef = React.useRef<HTMLDivElement>(null);
  const widgetIdRef = React.useRef<string | null>(null);
  const onVerifyRef = React.useRef(onVerify);
  const onExpireRef = React.useRef(onExpire);
  onVerifyRef.current = onVerify;
  onExpireRef.current = onExpire;

  React.useEffect(() => {
    let cancelled = false;

    loadTurnstileScript()
      .then(() => {
        if (cancelled || !containerRef.current || !window.turnstile) {
          return;
        }

        widgetIdRef.current = window.turnstile.render(containerRef.current, {
          sitekey: process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY || FALLBACK_SITE_KEY,
          theme: "auto",
          callback: (token) => onVerifyRef.current(token),
          "expired-callback": () => onExpireRef.current?.(),
        });
      })
      .catch(() => {
        // Sem Turnstile (bloqueado por extensao/rede) -- o botao de submit
        // fica desabilitado esperando um token que nunca chega; aceitavel
        // como falha fechada, nao ha fallback seguro pra "deixar passar".
      });

    return () => {
      cancelled = true;
      if (widgetIdRef.current && window.turnstile) {
        window.turnstile.remove(widgetIdRef.current);
      }
    };
  }, []);

  return <div ref={containerRef} />;
}
