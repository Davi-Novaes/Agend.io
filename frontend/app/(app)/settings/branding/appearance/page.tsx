"use client";

import * as React from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ImageIcon, Palette, Type, Upload } from "lucide-react";

import {
  updateTenantBranding,
  updateTenantPageCustomization,
  uploadTenantLogo,
  uploadTenantBanner,
  resolveAssetUrl,
  TENANT_PROFILE_QUERY_KEY,
  ApiError,
  type PublicPageFont,
  type PublicPageButtonStyle,
} from "@/lib/api/client";
import { meetsAaContrast, contrastRatio } from "@/lib/tenant/contrast";
import { useSession } from "@/lib/auth/session-context";
import { useBrandingDraft } from "@/components/settings/branding/branding-draft-context";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

const ALLOWED_IMAGE_TYPES = ["image/png", "image/jpeg", "image/webp"];
const MAX_LOGO_SIZE_BYTES = 2 * 1024 * 1024;
const MAX_BANNER_SIZE_BYTES = 4 * 1024 * 1024;

// A UI sempre pareia --primary com texto branco (ver app/globals.css) — o
// contraste e verificado contra ESTA cor fixa, tanto aqui quanto no backend.
const FOREGROUND_HEX = "#FFFFFF";

const FONT_OPTIONS: { value: PublicPageFont; label: string }[] = [
  { value: "Default", label: "Padrão do sistema" },
  { value: "Inter", label: "Inter" },
  { value: "Poppins", label: "Poppins" },
  { value: "Montserrat", label: "Montserrat" },
  { value: "PlayfairDisplay", label: "Playfair Display" },
  { value: "Lora", label: "Lora" },
  { value: "Merriweather", label: "Merriweather" },
];

const BUTTON_STYLE_OPTIONS: { value: PublicPageButtonStyle; label: string }[] = [
  { value: "Rounded", label: "Arredondado" },
  { value: "Square", label: "Reto" },
  { value: "Pill", label: "Pílula" },
];

// Hierarquia label/valor: rotulo pequeno/discreto/uppercase, valor grande/forte
// — mesma escala usada nas outras sub-abas de Marca (Conteudo/Informacoes).
const FIELD_LABEL_CLASS = "text-[13px] font-medium tracking-wide text-muted-foreground uppercase";
const FIELD_VALUE_CLASS = "text-[15px] font-semibold text-foreground";

export default function BrandingAppearancePage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const queryClient = useQueryClient();
  const { draft, update } = useBrandingDraft();

  const [isSavingColor, setIsSavingColor] = React.useState(false);
  const [isSavingCustomization, setIsSavingCustomization] = React.useState(false);
  const [selectedLogoFile, setSelectedLogoFile] = React.useState<File | null>(null);
  const [isUploadingLogo, setIsUploadingLogo] = React.useState(false);
  const logoInputRef = React.useRef<HTMLInputElement>(null);
  const [selectedBannerFile, setSelectedBannerFile] = React.useState<File | null>(null);
  const [isUploadingBanner, setIsUploadingBanner] = React.useState(false);
  const bannerInputRef = React.useRef<HTMLInputElement>(null);

  React.useEffect(() => {
    return () => {
      if (draft.logoPreviewUrl?.startsWith("blob:")) URL.revokeObjectURL(draft.logoPreviewUrl);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- so limpa no unmount, nao a cada troca de preview.
  }, []);

  const color = draft.primaryColorHex || "#3730A3";
  const ratio = contrastRatio(FOREGROUND_HEX, color);
  const passesAa = meetsAaContrast(FOREGROUND_HEX, color);

  const hasSecondaryColor = draft.secondaryColorHex.trim() !== "";
  const secondaryRatio = hasSecondaryColor ? contrastRatio(FOREGROUND_HEX, draft.secondaryColorHex) : null;
  const secondaryPassesAa = !hasSecondaryColor || meetsAaContrast(FOREGROUND_HEX, draft.secondaryColorHex);

  async function handleSaveColor() {
    setIsSavingColor(true);
    try {
      await updateTenantBranding(color, accessToken);
      // Faltava isto: sem invalidar, o resto do app (sidebar, fundo animado,
      // cards) so pegava a cor nova depois de um F5, porque cada um le o
      // profile do proprio cache do React Query.
      await queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY });
      toast.success("Cor de marca atualizada.");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar a cor de marca.");
    } finally {
      setIsSavingColor(false);
    }
  }

  async function handleSaveCustomization() {
    setIsSavingCustomization(true);
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
      toast.success("Aparência atualizada.");
      queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY });
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar.");
    } finally {
      setIsSavingCustomization(false);
    }
  }

  function handleLogoFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      toast.error("Formato inválido. Envie um arquivo PNG, JPEG ou WEBP.");
      return;
    }
    if (file.size > MAX_LOGO_SIZE_BYTES) {
      toast.error("O arquivo não pode ter mais que 2MB.");
      return;
    }
    setSelectedLogoFile(file);
    update({ logoPreviewUrl: URL.createObjectURL(file) });
  }

  async function handleLogoUpload() {
    if (!selectedLogoFile) return;
    setIsUploadingLogo(true);
    try {
      const result = await uploadTenantLogo(selectedLogoFile, accessToken);
      update({ logoPreviewUrl: resolveAssetUrl(result.logoUrl) });
      setSelectedLogoFile(null);
      queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY });
      toast.success("Logo atualizado.");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível enviar o logo.");
    } finally {
      setIsUploadingLogo(false);
    }
  }

  function handleBannerFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      toast.error("Formato inválido. Envie um arquivo PNG, JPEG ou WEBP.");
      return;
    }
    if (file.size > MAX_BANNER_SIZE_BYTES) {
      toast.error("O arquivo não pode ter mais que 4MB.");
      return;
    }
    setSelectedBannerFile(file);
    update({ bannerPreviewUrl: URL.createObjectURL(file) });
  }

  async function handleBannerUpload() {
    if (!selectedBannerFile) return;
    setIsUploadingBanner(true);
    try {
      const result = await uploadTenantBanner(selectedBannerFile, accessToken);
      update({ bannerPreviewUrl: resolveAssetUrl(result.bannerUrl) });
      setSelectedBannerFile(null);
      queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY });
      toast.success("Banner atualizado.");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Não foi possível enviar o banner.");
    } finally {
      setIsUploadingBanner(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader className="flex flex-row items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><ImageIcon className="size-5" /></span>
          <div className="space-y-1"><CardTitle className="text-base">Logo</CardTitle><CardDescription>A identidade que aparece no painel e na página pública.</CardDescription></div>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-4 rounded-xl border border-dashed bg-muted/15 p-4">
            <div className="bg-background relative flex size-20 shrink-0 flex-col items-center justify-center overflow-hidden rounded-xl border shadow-sm">
              <ImageIcon className="size-5 text-muted-foreground" />
              <span className="mt-1 text-[10px] text-muted-foreground">Sem prévia</span>
              {draft.logoPreviewUrl && (
                // eslint-disable-next-line @next/next/no-img-element -- preview de upload local/URL dinamica da API.
                <img key={draft.logoPreviewUrl} src={draft.logoPreviewUrl} alt="Logo do estabelecimento" className="absolute inset-0 size-full bg-background object-contain" onError={(event) => { event.currentTarget.style.display = "none"; }} />
              )}
            </div>
            <div className="flex flex-col gap-2">
              <input ref={logoInputRef} type="file" accept="image/png,image/jpeg,image/webp" onChange={handleLogoFileChange} className="hidden" />
              <div className="flex gap-2">
                <Button type="button" variant="outline" onClick={() => logoInputRef.current?.click()}>
                  <Upload className="size-4" />
                  Escolher arquivo
                </Button>
                {selectedLogoFile && (
                  <Button type="button" onClick={handleLogoUpload} disabled={isUploadingLogo}>
                    {isUploadingLogo ? "Enviando..." : "Enviar"}
                  </Button>
                )}
              </div>
              <p className="text-muted-foreground text-xs">PNG, JPEG ou WEBP, até 2MB.</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><ImageIcon className="size-5" /></span>
          <div className="space-y-1"><CardTitle className="text-base">Banner</CardTitle><CardDescription>A imagem de capa que recebe seus clientes.</CardDescription></div>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-4">
            <div className="bg-muted/30 relative flex h-36 w-full flex-col items-center justify-center overflow-hidden rounded-xl border border-dashed sm:h-44">
              <ImageIcon className="size-6 text-muted-foreground" />
              <span className="mt-2 text-xs text-muted-foreground">Envie uma imagem horizontal para a capa</span>
              {draft.bannerPreviewUrl && (
                // eslint-disable-next-line @next/next/no-img-element -- preview de upload local/URL dinamica da API.
                <img key={draft.bannerPreviewUrl} src={draft.bannerPreviewUrl} alt="Banner do estabelecimento" className="absolute inset-0 size-full bg-muted object-cover" onError={(event) => { event.currentTarget.style.display = "none"; }} />
              )}
            </div>
            <div className="flex flex-col gap-2">
              <input ref={bannerInputRef} type="file" accept="image/png,image/jpeg,image/webp" onChange={handleBannerFileChange} className="hidden" />
              <div className="flex gap-2">
                <Button type="button" variant="outline" onClick={() => bannerInputRef.current?.click()}>
                  <Upload className="size-4" />
                  Escolher arquivo
                </Button>
                {selectedBannerFile && (
                  <Button type="button" onClick={handleBannerUpload} disabled={isUploadingBanner}>
                    {isUploadingBanner ? "Enviando..." : "Enviar"}
                  </Button>
                )}
              </div>
              <p className="text-muted-foreground text-xs">PNG, JPEG ou WEBP, até 4MB.</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Palette className="size-5" /></span>
          <div className="space-y-1"><CardTitle className="text-base">Paleta de cores</CardTitle><CardDescription>Defina as cores dos botões, destaques e detalhes.</CardDescription></div>
        </CardHeader>
        <CardContent className="grid gap-6">
          <div className="grid gap-3">
            <div className="flex items-center gap-3">
              <input
                type="color"
                value={color}
                onChange={(event) => update({ primaryColorHex: event.target.value.toUpperCase() })}
                className="h-10 w-14 cursor-pointer rounded-md border border-input"
                aria-label="Selecionar cor principal"
              />
              <div className="grid gap-1.5">
                <Label htmlFor="color-hex" className={FIELD_LABEL_CLASS}>
                  Cor principal
                </Label>
                <input
                  id="color-hex"
                  value={color}
                  onChange={(event) => update({ primaryColorHex: event.target.value.toUpperCase() })}
                  maxLength={7}
                  className={`border-input bg-background h-8 w-28 rounded-md border px-2 font-mono outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 ${FIELD_VALUE_CLASS}`}
                />
              </div>
            </div>
            <p className={passesAa ? "text-sm text-emerald-700 dark:text-emerald-400" : "text-destructive text-sm"} role="status">
              Contraste com o texto branco: {ratio.toFixed(1)}:1 —{" "}
              {passesAa ? "atende ao padrão de acessibilidade (AA)." : "insuficiente. Escolha um tom mais escuro."}
            </p>
            <Button onClick={handleSaveColor} disabled={isSavingColor || !passesAa} className="w-fit">
              {isSavingColor ? "Salvando..." : "Salvar cor principal"}
            </Button>
          </div>

          <div className="grid gap-3 border-t pt-6">
            <div className="flex items-center gap-3">
              <input
                type="color"
                value={hasSecondaryColor ? draft.secondaryColorHex : "#64748B"}
                onChange={(event) => update({ secondaryColorHex: event.target.value.toUpperCase() })}
                className="h-10 w-14 cursor-pointer rounded-md border border-input"
                aria-label="Selecionar cor secundária"
              />
              <div className="grid gap-1.5">
                <Label htmlFor="secondary-color-hex" className={FIELD_LABEL_CLASS}>
                  Cor de apoio (opcional)
                </Label>
                <div className="flex items-center gap-2">
                  <input
                    id="secondary-color-hex"
                    value={draft.secondaryColorHex}
                    onChange={(event) => update({ secondaryColorHex: event.target.value.toUpperCase() })}
                    placeholder="Sem cor de apoio"
                    maxLength={7}
                    className={`border-input bg-background h-8 w-32 rounded-md border px-2 font-mono outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 ${FIELD_VALUE_CLASS}`}
                  />
                  {hasSecondaryColor && (
                    <Button type="button" variant="ghost" size="sm" onClick={() => update({ secondaryColorHex: "" })}>
                      Remover
                    </Button>
                  )}
                </div>
              </div>
            </div>
            {hasSecondaryColor && (
              <p className={secondaryPassesAa ? "text-sm text-emerald-700 dark:text-emerald-400" : "text-destructive text-sm"} role="status">
                Contraste com o texto branco: {secondaryRatio!.toFixed(1)}:1 —{" "}
                {secondaryPassesAa ? "atende ao padrão de acessibilidade (AA)." : "insuficiente. Escolha um tom mais escuro."}
              </p>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Type className="size-5" /></span>
          <div className="space-y-1"><CardTitle className="text-base">Tipografia e botões</CardTitle><CardDescription>Escolha a personalidade visual dos textos e ações.</CardDescription></div>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label className={FIELD_LABEL_CLASS}>Fonte</Label>
              <Select value={draft.font} onValueChange={(value) => update({ font: value as PublicPageFont })}>
                <SelectTrigger className={`w-full ${FIELD_VALUE_CLASS}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {FONT_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-1.5">
              <Label className={FIELD_LABEL_CLASS}>Estilo de botão</Label>
              <Select value={draft.buttonStyle} onValueChange={(value) => update({ buttonStyle: value as PublicPageButtonStyle })}>
                <SelectTrigger className={`w-full ${FIELD_VALUE_CLASS}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {BUTTON_STYLE_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <Button onClick={handleSaveCustomization} disabled={isSavingCustomization || !secondaryPassesAa} className="w-fit">
            {isSavingCustomization ? "Salvando..." : "Salvar aparência"}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
