"use client";

import * as React from "react";

import type { PublicPageButtonStyle, PublicPageFont, TenantProfile } from "@/lib/api/client";
import { resolveAssetUrl } from "@/lib/api/client";

// Estado de edicao EM MEMORIA, compartilhado entre as sub-abas de Marca
// (Aparencia/Conteudo/Informacoes) e o painel de previa ao vivo (ver
// live-preview-panel.tsx) -- nao e persistido em lugar nenhum sozinho, cada
// aba continua com seu proprio botao de salvar/mutation (mesmo espirito de
// "cada card sua propria acao" da pagina antiga). Isto so existe pra previa
// reagir instantaneamente enquanto o usuario digita, sem round-trip ao
// servidor nem reload de pagina.
export type BrandingDraft = {
  name: string;
  slug: string;
  primaryColorHex: string;
  secondaryColorHex: string;
  font: PublicPageFont;
  buttonStyle: PublicPageButtonStyle;
  logoPreviewUrl: string | null;
  bannerPreviewUrl: string | null;
  description: string;
  homeHeroTitle: string;
  homeHeroDescription: string;
  homeCtaText: string;
  showAboutSection: boolean;
  showServicesSection: boolean;
  showTeamSection: boolean;
  showHoursSection: boolean;
  showContactSection: boolean;
  publicPageEnabled: boolean;
};

type BrandingDraftContextValue = {
  draft: BrandingDraft;
  update: (patch: Partial<BrandingDraft>) => void;
};

const BrandingDraftContext = React.createContext<BrandingDraftContextValue | null>(null);

export function draftFromProfile(profile: TenantProfile): BrandingDraft {
  return {
    name: profile.name,
    slug: profile.slug,
    primaryColorHex: profile.primaryColorHex ?? "",
    secondaryColorHex: profile.secondaryColorHex ?? "",
    font: profile.font,
    buttonStyle: profile.buttonStyle,
    logoPreviewUrl: profile.logoUrl ? resolveAssetUrl(profile.logoUrl) : null,
    bannerPreviewUrl: profile.bannerUrl ? resolveAssetUrl(profile.bannerUrl) : null,
    description: profile.description ?? "",
    homeHeroTitle: profile.homeHeroTitle ?? "",
    homeHeroDescription: profile.homeHeroDescription ?? "",
    homeCtaText: profile.homeCtaText ?? "",
    showAboutSection: profile.showAboutSection,
    showServicesSection: profile.showServicesSection,
    showTeamSection: profile.showTeamSection,
    showHoursSection: profile.showHoursSection,
    showContactSection: profile.showContactSection,
    publicPageEnabled: profile.publicPageEnabled,
  };
}

export function BrandingDraftProvider({ profile, children }: { profile: TenantProfile; children: React.ReactNode }) {
  const [draft, setDraft] = React.useState<BrandingDraft>(() => draftFromProfile(profile));

  const update = React.useCallback((patch: Partial<BrandingDraft>) => {
    setDraft((current) => ({ ...current, ...patch }));
  }, []);

  const value = React.useMemo(() => ({ draft, update }), [draft, update]);

  return <BrandingDraftContext.Provider value={value}>{children}</BrandingDraftContext.Provider>;
}

export function useBrandingDraft(): BrandingDraftContextValue {
  const context = React.useContext(BrandingDraftContext);
  if (!context) {
    throw new Error("useBrandingDraft precisa ser usado dentro de BrandingDraftProvider.");
  }
  return context;
}
