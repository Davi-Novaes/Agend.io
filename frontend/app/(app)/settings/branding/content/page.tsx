"use client";

import * as React from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { updateTenantPageCustomization, TENANT_PROFILE_QUERY_KEY, ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { useBrandingDraft } from "@/components/settings/branding/branding-draft-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { meetsAaContrast } from "@/lib/tenant/contrast";

const FOREGROUND_HEX = "#FFFFFF";

export default function BrandingContentPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const queryClient = useQueryClient();
  const { draft, update } = useBrandingDraft();
  const [isSaving, setIsSaving] = React.useState(false);

  const hasSecondaryColor = draft.secondaryColorHex.trim() !== "";
  const secondaryPassesAa = !hasSecondaryColor || meetsAaContrast(FOREGROUND_HEX, draft.secondaryColorHex);

  async function handleSave() {
    setIsSaving(true);
    try {
      await updateTenantPageCustomization(
        {
          secondaryColorHex: hasSecondaryColor ? draft.secondaryColorHex : null,
          font: draft.font,
          buttonStyle: draft.buttonStyle,
          showAboutSection: draft.showAboutSection,
          showServicesSection: draft.showServicesSection,
          showTeamSection: draft.showTeamSection,
          showHoursSection: draft.showHoursSection,
          showContactSection: draft.showContactSection,
          homeHeroTitle: draft.homeHeroTitle || null,
          homeHeroDescription: draft.homeHeroDescription || null,
          homeCtaText: draft.homeCtaText || null,
        },
        accessToken
      );
      toast.success("Conteúdo atualizado.");
      queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY });
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Página inicial</CardTitle>
          <CardDescription>Título, descrição e texto do botão exibidos no topo da sua página pública.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="grid gap-1">
            <Label htmlFor="hero-title">Título de destaque</Label>
            <Input
              id="hero-title"
              placeholder={`Agende seu horário na ${draft.name}`}
              value={draft.homeHeroTitle}
              onChange={(event) => update({ homeHeroTitle: event.target.value })}
              maxLength={200}
            />
          </div>
          <div className="grid gap-1">
            <Label htmlFor="hero-description">Descrição</Label>
            <Textarea
              id="hero-description"
              rows={3}
              placeholder="Escolha um serviço e reserve seu horário."
              value={draft.homeHeroDescription}
              onChange={(event) => update({ homeHeroDescription: event.target.value })}
              maxLength={1000}
            />
          </div>
          <div className="grid max-w-xs gap-1">
            <Label htmlFor="hero-cta">Texto do botão</Label>
            <Input
              id="hero-cta"
              placeholder="Agendar horario"
              value={draft.homeCtaText}
              onChange={(event) => update({ homeCtaText: event.target.value })}
              maxLength={60}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">O que aparece na página pública</CardTitle>
          <CardDescription>Mostre ou esconda seções inteiras — o conteúdo continua vindo dos seus cadastros reais.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <div className="flex items-center justify-between gap-4">
            <span className="text-sm">Sobre (descrição no topo)</span>
            <Switch checked={draft.showAboutSection} onCheckedChange={(value) => update({ showAboutSection: value })} aria-label="Mostrar seção Sobre" />
          </div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-sm">Serviços</span>
            <Switch checked={draft.showServicesSection} onCheckedChange={(value) => update({ showServicesSection: value })} aria-label="Mostrar seção Serviços" />
          </div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-sm">Equipe</span>
            <Switch checked={draft.showTeamSection} onCheckedChange={(value) => update({ showTeamSection: value })} aria-label="Mostrar seção Equipe" />
          </div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-sm">Horário de funcionamento</span>
            <Switch checked={draft.showHoursSection} onCheckedChange={(value) => update({ showHoursSection: value })} aria-label="Mostrar seção Horário" />
          </div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-sm">Contato e redes sociais</span>
            <Switch checked={draft.showContactSection} onCheckedChange={(value) => update({ showContactSection: value })} aria-label="Mostrar seção Contato" />
          </div>
        </CardContent>
      </Card>

      <Button onClick={handleSave} disabled={isSaving || !secondaryPassesAa} className="w-fit">
        {isSaving ? "Salvando..." : "Salvar conteúdo"}
      </Button>
    </div>
  );
}
