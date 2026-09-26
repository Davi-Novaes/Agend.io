"use client";

import * as React from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { BriefcaseBusiness, Clock3, Contact, TextCursorInput, Users } from "lucide-react";

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

// Hierarquia label/valor: rotulo pequeno/discreto/uppercase, valor grande/forte.
const FIELD_LABEL_CLASS = "text-[13px] font-medium tracking-wide text-muted-foreground uppercase";
const FIELD_VALUE_CLASS = "text-[15px] font-semibold text-foreground";

export default function BrandingContentPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const queryClient = useQueryClient();
  const { draft, update } = useBrandingDraft();
  const [isSaving, setIsSaving] = React.useState(false);

  const hasSecondaryColor = draft.secondaryColorHex.trim() !== "";
  const secondaryPassesAa = !hasSecondaryColor || meetsAaContrast(FOREGROUND_HEX, draft.secondaryColorHex);
  const sectionOptions = [
    { label: "Sobre o negócio", description: "Apresentação exibida perto do topo", icon: TextCursorInput, checked: draft.showAboutSection, key: "showAboutSection" as const },
    { label: "Serviços", description: "Catálogo de serviços disponíveis", icon: BriefcaseBusiness, checked: draft.showServicesSection, key: "showServicesSection" as const },
    { label: "Equipe", description: "Profissionais disponíveis para agendamento", icon: Users, checked: draft.showTeamSection, key: "showTeamSection" as const },
    { label: "Horário de funcionamento", description: "Dias e horários em que você atende", icon: Clock3, checked: draft.showHoursSection, key: "showHoursSection" as const },
    { label: "Contato e redes sociais", description: "Telefone, endereço e seus perfis", icon: Contact, checked: draft.showContactSection, key: "showContactSection" as const },
  ];

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
          <div className="grid gap-1.5">
            <Label htmlFor="hero-title" className={FIELD_LABEL_CLASS}>
              Título de destaque
            </Label>
            <Input
              id="hero-title"
              placeholder={`Agende seu horário na ${draft.name}`}
              value={draft.homeHeroTitle}
              onChange={(event) => update({ homeHeroTitle: event.target.value })}
              maxLength={200}
              className={FIELD_VALUE_CLASS}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="hero-description" className={FIELD_LABEL_CLASS}>
              Descrição
            </Label>
            <Textarea
              id="hero-description"
              rows={3}
              placeholder="Escolha um serviço e reserve seu horário."
              value={draft.homeHeroDescription}
              onChange={(event) => update({ homeHeroDescription: event.target.value })}
              maxLength={1000}
              className={FIELD_VALUE_CLASS}
            />
          </div>
          <div className="grid max-w-xs gap-1.5">
            <Label htmlFor="hero-cta" className={FIELD_LABEL_CLASS}>
              Texto do botão
            </Label>
            <Input
              id="hero-cta"
              placeholder="Agendar horario"
              value={draft.homeCtaText}
              onChange={(event) => update({ homeCtaText: event.target.value })}
              maxLength={60}
              className={FIELD_VALUE_CLASS}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">O que aparece na página pública</CardTitle>
          <CardDescription>Mostre ou esconda seções inteiras — o conteúdo continua vindo dos seus cadastros reais.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-2.5">
          {sectionOptions.map((section) => {
            const Icon = section.icon;
            return (
              <div key={section.key} className="flex items-center justify-between gap-4 rounded-xl border bg-muted/20 p-3.5 transition-colors hover:bg-muted/35">
                <div className="flex min-w-0 items-center gap-3">
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><Icon className="size-4" /></span>
                  <span className="min-w-0"><span className="block text-sm font-medium">{section.label}</span><span className="block truncate text-xs text-muted-foreground">{section.description}</span></span>
                </div>
                <Switch checked={section.checked} onCheckedChange={(value) => update({ [section.key]: value })} aria-label={`Mostrar seção ${section.label}`} />
              </div>
            );
          })}
        </CardContent>
      </Card>

      <Button onClick={handleSave} disabled={isSaving || !secondaryPassesAa} className="w-fit">
        {isSaving ? "Salvando..." : "Salvar conteúdo"}
      </Button>
    </div>
  );
}
