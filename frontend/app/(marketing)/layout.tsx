import type { ReactNode } from "react";
import { ScopedThemeProvider } from "@/lib/theme/scoped-theme";

// "marketing-shell" da o gancho de CSS pro preto puro (--background: #000,
// ver ".marketing-shell.dark" no globals.css) -- diferente do resto do app,
// que usa o tom mais claro (#08080C) no dark mode do painel.
//
// storageKey proprio (ScopedThemeProvider, nao next-themes -- ver
// lib/theme/scoped-theme.tsx pro motivo): o toggle de tema aqui (ver
// components/marketing/marketing-theme-toggle.tsx) e independente do painel
// autenticado (app/(app)/layout.tsx, via next-themes) e do login (sempre
// fixo, ver app/(auth)/login/page.tsx) -- trocar um nunca afeta os outros.
const MARKETING_THEME_STORAGE_KEY = "agendio-marketing-theme";

export default function MarketingLayout({ children }: { children: ReactNode }) {
  // bg-background/text-foreground aqui (nao so a classe marker): essas duas
  // CSS vars so valem a partir de onde sao redeclaradas pra baixo -- sem
  // isto, este wrapper herdaria background E COR DE TEXTO do <body> (fora do
  // escopo do ScopedThemeProvider) em vez dos proprios. `color` (diferente de
  // uma custom property) e herdado como VALOR JA COMPUTADO, entao so
  // redeclarar --foreground em .theme-fixed-light/.dark (globals.css) nao
  // basta -- sem um `color: var(--foreground)` aqui pra forcar o recalculo,
  // o texto inteiro da home ficava branco (herdado do <body> escuro) sobre
  // fundo claro sempre que o dono deixava o painel autenticado em dark mode
  // antes de visitar a home (bug real, reproduzido e confirmado antes deste
  // fix). Mesma tecnica ja usada em app/(auth)/login/page.tsx. min-h-full
  // garante que a pintura cobre a pagina inteira, nao so a altura do conteudo.
  return (
    <ScopedThemeProvider
      storageKey={MARKETING_THEME_STORAGE_KEY}
      className="marketing-shell bg-background text-foreground min-h-full"
    >
      {children}
    </ScopedThemeProvider>
  );
}
