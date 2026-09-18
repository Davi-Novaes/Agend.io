import { CursorSpotlight } from "@/components/cursor-spotlight";

/** Fundo ambiente do painel interno -- so atras do conteudo (SidebarInset), nunca atras da sidebar (ver app/(app)/layout.tsx e globals.css). */
export function AppBackground() {
  return (
    <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
      <div className="app-bg-glow" />
      <div className="app-bg-lines" />
      {/* Bem menor que o padrao (480px) usado no hero/onboarding -- aqui e so um toque discreto, nao o efeito principal da tela. */}
      <CursorSpotlight size={140} />
    </div>
  );
}
