"use client";

import { ExternalLink, Sparkles } from "lucide-react";

import type { PublicPageButtonStyle, PublicPageFont } from "@/lib/api/client";
import { useBrandingDraft } from "@/components/settings/branding/branding-draft-context";
import { Badge } from "@/components/ui/badge";

// Mapa de fonte -> CSS font-family (a pagina publica de verdade usa
// next/font/google -- aqui e so uma previa visual, entao basta carregar as
// mesmas fontes via <link> e referenciar pelo nome; Next.js hoisteia
// automaticamente qualquer <link rel="stylesheet"> renderizado na arvore pro
// <head>, mesmo vindo de um componente filho).
const FONT_FAMILY_BY_VALUE: Record<PublicPageFont, string> = {
  Default: "ui-sans-serif, system-ui, sans-serif",
  Inter: "'Inter', sans-serif",
  Poppins: "'Poppins', sans-serif",
  Montserrat: "'Montserrat', sans-serif",
  PlayfairDisplay: "'Playfair Display', serif",
  Lora: "'Lora', serif",
  Merriweather: "'Merriweather', serif",
};

const BUTTON_RADIUS_BY_STYLE: Record<PublicPageButtonStyle, string> = {
  Rounded: "0.5rem",
  Square: "0.125rem",
  Pill: "9999px",
};

const DEFAULT_PRIMARY = "#3730A3";

export function LivePreviewFonts() {
  return (
    // eslint-disable-next-line @next/next/no-page-custom-font -- regra pensada pro Pages Router (pages/_document.js nao existe aqui); App Router hoisteia <link rel="stylesheet"> normalmente pro <head>. So previa, a pagina publica de verdade usa next/font/google.
    <link
      rel="stylesheet"
      href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&family=Poppins:wght@400;600;700&family=Montserrat:wght@400;600;700&family=Playfair+Display:wght@400;700&family=Lora:wght@400;700&family=Merriweather:wght@400;700&display=swap"
    />
  );
}

export function LivePreviewPanel({ compact = false }: { compact?: boolean }) {
  const { draft } = useBrandingDraft();

  const primary = draft.primaryColorHex || DEFAULT_PRIMARY;
  const secondary = draft.secondaryColorHex || null;
  const fontFamily = FONT_FAMILY_BY_VALUE[draft.font];
  const buttonRadius = BUTTON_RADIUS_BY_STYLE[draft.buttonStyle];
  const heroTitle = draft.homeHeroTitle.trim() || `Agende seu horário na ${draft.name}`;
  const heroDescription = draft.homeHeroDescription.trim() || draft.description.trim() || "Escolha um serviço e reserve seu horário.";
  const ctaText = draft.homeCtaText.trim() || "Agendar horario";

  return (
    <div className="flex flex-col gap-2" style={{ fontFamily }}>
      <LivePreviewFonts />
      <div className="text-muted-foreground flex items-center justify-between text-xs">
        <span className="flex items-center gap-1.5 font-medium"><span className="size-1.5 rounded-full bg-primary" />Prévia ao vivo</span>
        <Badge variant={draft.publicPageEnabled ? "success" : "secondary"}>
          {draft.publicPageEnabled ? "Publicada" : "Despublicada"}
        </Badge>
      </div>

      <div className="overflow-hidden rounded-2xl border bg-background shadow-xl shadow-black/5 ring-1 ring-white/5">
        {/* Hero — mesmo tratamento da pagina publica de verdade
            (app/(public)/[slug]/page.tsx): banner em opacidade cheia com um
            degrade escuro por cima so pra legibilidade do texto, nunca a cor
            de marca lavando o banner (antes usava opacity-30 na imagem, que
            deixava a cor dominando e o banner quase invisivel). Sem banner,
            a cor de marca continua sendo o fundo. */}
        <div
          className="relative flex flex-col items-center gap-3 px-6 py-10 text-center"
          style={{ backgroundColor: primary, color: "#FFFFFF" }}
        >
          {draft.bannerPreviewUrl && (
            <>
              {/* eslint-disable-next-line @next/next/no-img-element -- previa local/blob, nao asset estatico do build. */}
              <img key={draft.bannerPreviewUrl} src={draft.bannerPreviewUrl} alt="" className="absolute inset-0 size-full object-cover" onError={(event) => { event.currentTarget.style.display = "none"; }} />
              <div aria-hidden className="absolute inset-0 bg-gradient-to-t from-black/75 via-black/35 to-black/10" />
            </>
          )}
          <div className="relative flex flex-col items-center gap-3">
            <div className="relative flex size-14 items-center justify-center overflow-hidden rounded-xl bg-white shadow">
              <Sparkles className="size-6" style={{ color: primary }} />
              {draft.logoPreviewUrl && (
                // eslint-disable-next-line @next/next/no-img-element -- previa local/blob, nao asset estatico do build.
                <img key={draft.logoPreviewUrl} src={draft.logoPreviewUrl} alt="" className="absolute inset-0 size-full bg-white object-contain" onError={(event) => { event.currentTarget.style.display = "none"; }} />
              )}
            </div>
            <div>
              <p className="text-lg font-semibold text-balance">{heroTitle}</p>
              {!compact && <p className="mt-1 text-sm text-white/85 text-balance">{heroDescription}</p>}
            </div>
            <button
              type="button"
              disabled
              className="px-4 py-2 text-sm font-medium shadow-sm"
              style={{ backgroundColor: "#FFFFFF", color: primary, borderRadius: buttonRadius }}
            >
              {ctaText}
            </button>
          </div>
        </div>

        {!compact && draft.showServicesSection && (
          <div className="bg-background flex flex-col gap-2 border-t p-4">
            <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">Serviços</p>
            <div className="grid grid-cols-2 gap-2">
              {["Corte", "Barba"].map((service) => (
                <div key={service} className="flex flex-col gap-1 rounded-lg border p-2 text-xs">
                  <span className="font-medium">{service}</span>
                  <span
                    className="w-fit rounded-full px-1.5 py-0.5 text-[10px]"
                    style={{ backgroundColor: (secondary ?? primary) + "1A", color: secondary ?? primary }}
                  >
                    a partir de R$ 40
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {!compact && draft.showContactSection && (
          <div className="text-muted-foreground flex items-center justify-center gap-1.5 border-t p-4 text-center text-xs">
            {draft.name} · página pública <ExternalLink className="size-3" />
          </div>
        )}
      </div>
    </div>
  );
}
