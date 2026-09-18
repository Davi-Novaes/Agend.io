"use client";

import * as React from "react";

import { cn } from "@/lib/utils";

// next-themes (ver app/providers.tsx) NAO serve pra isolar o tema de uma
// area especifica: seu ThemeProvider vira um no-op (Fragment passthrough)
// quando ja existe um ThemeProvider ancestral (ver node_modules/next-themes/
// dist/index.js — `t.useContext(L) ? Fragment : <implementacao real>`), de
// proposito, pra evitar duas instancias brigando pelo <html>. Isso significa
// que aninhar um segundo <ThemeProvider> so pra pagina de marketing e um
// no-op silencioso: ele delega pro contexto raiz (o mesmo do painel
// autenticado), exatamente o vazamento que o usuario pediu pra eliminar.
//
// Esta e uma implementacao PROPRIA e independente, sem next-themes: guarda
// preferencia num storageKey exclusivo e aplica a classe "dark" (mesma usada
// pelo resto do design system, ver app/globals.css) num elemento PROPRIO
// (nao document.documentElement) — assim @custom-variant dark continua
// funcionando normalmente (ele so olha por QUALQUER ancestral com .dark,
// nao exige que seja o <html>), mas o escopo fica isolado aqui dentro.
export type ScopedThemePreference = "light" | "dark" | "system";

type ScopedThemeContextValue = {
  preference: ScopedThemePreference;
  resolvedTheme: "light" | "dark";
  setPreference: (preference: ScopedThemePreference) => void;
};

const ScopedThemeContext = React.createContext<ScopedThemeContextValue | null>(null);

function readStoredPreference(storageKey: string): ScopedThemePreference {
  if (typeof window === "undefined") return "system";
  try {
    const stored = localStorage.getItem(storageKey);
    if (stored === "light" || stored === "dark" || stored === "system") return stored;
  } catch {
    // localStorage indisponivel (modo privado etc.) -- fica no default.
  }
  return "system";
}

// useSyncExternalStore (em vez de useState+useEffect) porque o valor vem de
// uma fonte fora do React (matchMedia) -- e o jeito recomendado de ler estado
// externo sem disparar o set-state-em-efeito que o eslint acusa (renders em
// cascata). getServerSnapshot fixo em "false" mantem a SSR deterministica.
function subscribeToSystemScheme(onStoreChange: () => void) {
  const media = window.matchMedia("(prefers-color-scheme: dark)");
  media.addEventListener("change", onStoreChange);
  return () => media.removeEventListener("change", onStoreChange);
}

function getSystemSchemeSnapshot(): boolean {
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
}

function getSystemSchemeServerSnapshot(): boolean {
  return false;
}

export function useScopedTheme(): ScopedThemeContextValue {
  const context = React.useContext(ScopedThemeContext);
  if (!context) {
    throw new Error("useScopedTheme precisa ser usado dentro de ScopedThemeProvider.");
  }
  return context;
}

export function ScopedThemeProvider({
  storageKey,
  className,
  children,
}: {
  /** Chave de localStorage exclusiva desta area -- nunca compartilhar com outra ScopedThemeProvider nem com o "theme" do next-themes. */
  storageKey: string;
  className?: string;
  children: React.ReactNode;
}) {
  // Lazy initializer (nao efeito): roda direto no render, tanto no server
  // (retorna "system" por falta de window) quanto no client (le o valor real
  // ja na primeira renderizacao de hidratacao) -- suppressHydrationWarning no
  // wrapper abaixo absorve a divergencia de classe daí resultante.
  const [preference, setPreferenceState] = React.useState<ScopedThemePreference>(() =>
    readStoredPreference(storageKey)
  );

  const isSystemDark = React.useSyncExternalStore(
    subscribeToSystemScheme,
    getSystemSchemeSnapshot,
    getSystemSchemeServerSnapshot
  );

  const resolvedTheme: "light" | "dark" =
    preference === "system" ? (isSystemDark ? "dark" : "light") : preference;

  const setPreference = React.useCallback(
    (next: ScopedThemePreference) => {
      setPreferenceState(next);
      try {
        localStorage.setItem(storageKey, next);
      } catch {
        // localStorage indisponivel -- preferencia so dura a sessao atual.
      }
    },
    [storageKey]
  );

  const value = React.useMemo(() => ({ preference, resolvedTheme, setPreference }), [preference, resolvedTheme, setPreference]);

  // "dark" e a MESMA classe que o resto do design system usa (aciona
  // @custom-variant dark e o bloco ".dark {...}" do globals.css normalmente
  // -- so que aplicada aqui, nao em document.documentElement, entao fica
  // isolada). "theme-fixed-light" (ver globals.css, criada pro login) blinda
  // contra um ancestral ".dark" de outra area quando a resolucao AQUI e
  // clara -- sem isso, se o dono tiver dark mode no painel, esta area
  // herdaria as variaveis escuras mesmo escolhendo "claro" so pra ela.
  const themeClassName = resolvedTheme === "dark" ? "dark" : "theme-fixed-light";

  return (
    <ScopedThemeContext.Provider value={value}>
      <div className={cn(className, themeClassName)} suppressHydrationWarning>
        {children}
      </div>
    </ScopedThemeContext.Provider>
  );
}
