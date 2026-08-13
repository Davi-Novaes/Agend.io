"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Plus, Trash2 } from "lucide-react";

import {
  updateTenantBranding,
  uploadTenantLogo,
  uploadTenantBanner,
  getTenantProfile,
  updateTenantProfile,
  setTenantBusinessHours,
  updateTenantSchedulingSettings,
  updateTenantPageCustomization,
  resolveAssetUrl,
  ApiError,
  type TenantProfile,
  type DayOfWeekName,
  type PublicPageFont,
  type PublicPageButtonStyle,
} from "@/lib/api/client";
import { meetsAaContrast, contrastRatio } from "@/lib/tenant/contrast";
import { useSession } from "@/lib/auth/session-context";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const ALLOWED_LOGO_TYPES = ["image/png", "image/jpeg", "image/webp"];
const MAX_LOGO_SIZE_BYTES = 2 * 1024 * 1024;
const ALLOWED_BANNER_TYPES = ["image/png", "image/jpeg", "image/webp"];
const MAX_BANNER_SIZE_BYTES = 4 * 1024 * 1024;

// A UI sempre pareia --primary com texto branco (ver app/globals.css) — o
// contraste e verificado contra ESTA cor fixa, tanto aqui quanto no backend.
const FOREGROUND_HEX = "#FFFFFF";

function toHex(oklchOrHex: string): string {
  return oklchOrHex.startsWith("#") ? oklchOrHex : "#3730A3";
}

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

const FONT_OPTIONS: { value: PublicPageFont; label: string }[] = [
  { value: "Default", label: "Padrao do sistema" },
  { value: "Poppins", label: "Poppins" },
  { value: "PlayfairDisplay", label: "Playfair Display" },
  { value: "Merriweather", label: "Merriweather" },
];

const BUTTON_STYLE_OPTIONS: { value: PublicPageButtonStyle; label: string }[] = [
  { value: "Rounded", label: "Arredondado" },
  { value: "Square", label: "Reto" },
  { value: "Pill", label: "Pilula" },
];

const DAY_OPTIONS: { value: DayOfWeekName; label: string }[] = [
  { value: "Monday", label: "Segunda" },
  { value: "Tuesday", label: "Terca" },
  { value: "Wednesday", label: "Quarta" },
  { value: "Thursday", label: "Quinta" },
  { value: "Friday", label: "Sexta" },
  { value: "Saturday", label: "Sabado" },
  { value: "Sunday", label: "Domingo" },
];

const profileSchema = z.object({
  description: z.string(),
  phone: z.string(),
  whatsApp: z.string(),
  email: z.string(),
  address: z.string(),
  instagramUrl: z.string(),
  facebookUrl: z.string(),
});

type ProfileFormValues = z.infer<typeof profileSchema>;

const businessHoursSchema = z.object({
  entries: z.array(
    z.object({
      dayOfWeek: z.enum(["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]),
      startTime: z.string().min(1, "Informe o horario inicial."),
      endTime: z.string().min(1, "Informe o horario final."),
    })
  ),
});

type BusinessHoursFormValues = z.infer<typeof businessHoursSchema>;

const schedulingSettingsSchema = z.object({
  closedDates: z.array(
    z.object({
      date: z.string().min(1, "Informe a data."),
      reason: z.string(),
    })
  ),
  appointmentBufferMinutes: z.coerce.number().int().min(0, "Minimo 0.").max(240, "Maximo 240."),
});

type SchedulingSettingsFormInput = z.input<typeof schedulingSettingsSchema>;
type SchedulingSettingsFormValues = z.output<typeof schedulingSettingsSchema>;

function toTimeInputValue(time: string): string {
  return time.slice(0, 5);
}

function toApiTimeValue(time: string): string {
  return time.length === 5 ? `${time}:00` : time;
}

export default function BrandingSettingsPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";

  const profileQuery = useQuery({
    queryKey: ["tenant", "profile"],
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-lg flex-1 flex-col gap-6">
      <p className="text-muted-foreground text-sm">
        Personalize a identidade, o contato e o horario de funcionamento do seu estabelecimento.
      </p>

      {profileQuery.isLoading || !profileQuery.data ? (
        <div className="flex flex-col gap-6">
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-56 w-full" />
          <Skeleton className="h-72 w-full" />
          <Skeleton className="h-56 w-full" />
          <Skeleton className="h-72 w-full" />
        </div>
      ) : (
        // Sem key dinamica de proposito: os forms internos (useForm/useState) so usam
        // profile para o valor INICIAL, na primeira montagem — um refetch em segundo
        // plano (ex.: apos salvar, ou refetchOnWindowFocus) atualiza profileQuery.data
        // mas NAO deve remontar o formulario e descartar o que o usuario ja digitou.
        <BrandingForm profile={profileQuery.data} accessToken={accessToken} />
      )}
    </div>
  );
}

function BrandingForm({ profile, accessToken }: { profile: TenantProfile; accessToken: string }) {
  const queryClient = useQueryClient();

  const [color, setColor] = React.useState(() => toHex(profile.primaryColorHex ?? DEFAULT_TENANT_THEME.primary));
  const [isSavingColor, setIsSavingColor] = React.useState(false);
  const [logoPreviewUrl, setLogoPreviewUrl] = React.useState<string | null>(
    profile.logoUrl ? resolveAssetUrl(profile.logoUrl) : null
  );
  const [selectedLogoFile, setSelectedLogoFile] = React.useState<File | null>(null);
  const [isUploadingLogo, setIsUploadingLogo] = React.useState(false);
  const logoInputRef = React.useRef<HTMLInputElement>(null);

  const [bannerPreviewUrl, setBannerPreviewUrl] = React.useState<string | null>(
    profile.bannerUrl ? resolveAssetUrl(profile.bannerUrl) : null
  );
  const [selectedBannerFile, setSelectedBannerFile] = React.useState<File | null>(null);
  const [isUploadingBanner, setIsUploadingBanner] = React.useState(false);
  const bannerInputRef = React.useRef<HTMLInputElement>(null);

  const [secondaryColor, setSecondaryColor] = React.useState(profile.secondaryColorHex ?? "");
  const [font, setFont] = React.useState<PublicPageFont>(profile.font);
  const [buttonStyle, setButtonStyle] = React.useState<PublicPageButtonStyle>(profile.buttonStyle);
  const [showAboutSection, setShowAboutSection] = React.useState(profile.showAboutSection);
  const [showServicesSection, setShowServicesSection] = React.useState(profile.showServicesSection);
  const [showTeamSection, setShowTeamSection] = React.useState(profile.showTeamSection);
  const [showHoursSection, setShowHoursSection] = React.useState(profile.showHoursSection);
  const [showContactSection, setShowContactSection] = React.useState(profile.showContactSection);
  const [isSavingCustomization, setIsSavingCustomization] = React.useState(false);

  const profileForm = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      description: profile.description ?? "",
      phone: profile.phone ?? "",
      whatsApp: profile.whatsApp ?? "",
      email: profile.email ?? "",
      address: profile.address ?? "",
      instagramUrl: profile.instagramUrl ?? "",
      facebookUrl: profile.facebookUrl ?? "",
    },
  });

  const hoursForm = useForm<BusinessHoursFormValues>({
    resolver: zodResolver(businessHoursSchema),
    defaultValues: {
      entries: profile.businessHours.map((entry) => ({
        dayOfWeek: entry.dayOfWeek,
        startTime: toTimeInputValue(entry.startTime),
        endTime: toTimeInputValue(entry.endTime),
      })),
    },
  });

  const hoursFieldArray = useFieldArray({ control: hoursForm.control, name: "entries" });

  const schedulingSettingsForm = useForm<SchedulingSettingsFormInput, unknown, SchedulingSettingsFormValues>({
    resolver: zodResolver(schedulingSettingsSchema),
    defaultValues: {
      closedDates: profile.closedDates.map((entry) => ({ date: entry.date, reason: entry.reason ?? "" })),
      appointmentBufferMinutes: profile.appointmentBufferMinutes,
    },
  });

  const closedDatesFieldArray = useFieldArray({ control: schedulingSettingsForm.control, name: "closedDates" });

  React.useEffect(() => {
    return () => {
      if (logoPreviewUrl?.startsWith("blob:")) {
        URL.revokeObjectURL(logoPreviewUrl);
      }
    };
  }, [logoPreviewUrl]);

  React.useEffect(() => {
    return () => {
      if (bannerPreviewUrl?.startsWith("blob:")) {
        URL.revokeObjectURL(bannerPreviewUrl);
      }
    };
  }, [bannerPreviewUrl]);

  const profileMutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      updateTenantProfile(
        {
          description: toNullable(values.description),
          phone: toNullable(values.phone),
          whatsApp: toNullable(values.whatsApp),
          email: toNullable(values.email),
          address: toNullable(values.address),
          instagramUrl: toNullable(values.instagramUrl),
          facebookUrl: toNullable(values.facebookUrl),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Perfil do estabelecimento atualizado.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel salvar o perfil."),
  });

  const businessHoursMutation = useMutation({
    mutationFn: (values: BusinessHoursFormValues) => {
      const entries = values.entries.map((entry) => ({
        ...entry,
        startTime: toApiTimeValue(entry.startTime),
        endTime: toApiTimeValue(entry.endTime),
      }));
      return setTenantBusinessHours(entries, accessToken);
    },
    onSuccess: () => {
      toast.success("Horario de funcionamento atualizado.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel salvar o horario."),
  });

  const schedulingSettingsMutation = useMutation({
    mutationFn: (values: SchedulingSettingsFormValues) =>
      updateTenantSchedulingSettings(
        {
          closedDates: values.closedDates.map((entry) => ({ date: entry.date, reason: toNullable(entry.reason) })),
          appointmentBufferMinutes: values.appointmentBufferMinutes,
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Configuracoes de agendamento atualizadas.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel salvar as configuracoes de agendamento."),
  });

  function handleLogoFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    if (!ALLOWED_LOGO_TYPES.includes(file.type)) {
      toast.error("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
      return;
    }

    if (file.size > MAX_LOGO_SIZE_BYTES) {
      toast.error("O arquivo nao pode ter mais que 2MB.");
      return;
    }

    setSelectedLogoFile(file);
    setLogoPreviewUrl(URL.createObjectURL(file));
  }

  async function handleLogoUpload() {
    if (!selectedLogoFile) {
      return;
    }

    setIsUploadingLogo(true);
    try {
      const result = await uploadTenantLogo(selectedLogoFile, accessToken);
      setLogoPreviewUrl(resolveAssetUrl(result.logoUrl));
      setSelectedLogoFile(null);
      toast.success("Logo atualizado.");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel enviar o logo.";
      toast.error(message);
    } finally {
      setIsUploadingLogo(false);
    }
  }

  function handleBannerFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    if (!ALLOWED_BANNER_TYPES.includes(file.type)) {
      toast.error("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
      return;
    }

    if (file.size > MAX_BANNER_SIZE_BYTES) {
      toast.error("O arquivo nao pode ter mais que 4MB.");
      return;
    }

    setSelectedBannerFile(file);
    setBannerPreviewUrl(URL.createObjectURL(file));
  }

  async function handleBannerUpload() {
    if (!selectedBannerFile) {
      return;
    }

    setIsUploadingBanner(true);
    try {
      const result = await uploadTenantBanner(selectedBannerFile, accessToken);
      setBannerPreviewUrl(resolveAssetUrl(result.bannerUrl));
      setSelectedBannerFile(null);
      toast.success("Banner atualizado.");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel enviar o banner.";
      toast.error(message);
    } finally {
      setIsUploadingBanner(false);
    }
  }

  const ratio = contrastRatio(FOREGROUND_HEX, color);
  const passesAa = meetsAaContrast(FOREGROUND_HEX, color);

  async function handleSaveColor() {
    setIsSavingColor(true);
    try {
      await updateTenantBranding(color, accessToken);
      toast.success("Cor de marca atualizada.");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel salvar a cor de marca.";
      toast.error(message);
    } finally {
      setIsSavingColor(false);
    }
  }

  const hasSecondaryColor = secondaryColor.trim() !== "";
  const secondaryRatio = hasSecondaryColor ? contrastRatio(FOREGROUND_HEX, secondaryColor) : null;
  const secondaryPassesAa = !hasSecondaryColor || meetsAaContrast(FOREGROUND_HEX, secondaryColor);

  function currentCustomizationInput() {
    return {
      secondaryColorHex: hasSecondaryColor ? secondaryColor : null,
      font,
      buttonStyle,
      showAboutSection,
      showServicesSection,
      showTeamSection,
      showHoursSection,
      showContactSection,
    };
  }

  async function handleSaveCustomization() {
    setIsSavingCustomization(true);
    try {
      await updateTenantPageCustomization(currentCustomizationInput(), accessToken);
      toast.success("Personalizacao da pagina atualizada.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel salvar a personalizacao.";
      toast.error(message);
    } finally {
      setIsSavingCustomization(false);
    }
  }

  function handlePreview() {
    const encoded = btoa(JSON.stringify(currentCustomizationInput()));
    window.open(`/${profile.slug}?preview=${encoded}`, "_blank", "noopener,noreferrer");
  }

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Logo</CardTitle>
          <CardDescription>Aparece no painel e no portal publico do seu estabelecimento.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-4">
            <div className="bg-muted flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-lg border">
              {logoPreviewUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- preview de upload local/URL dinamica da API, nao um asset estatico do build.
                <img src={logoPreviewUrl} alt="Logo do estabelecimento" className="size-full object-contain" />
              ) : (
                <span className="text-muted-foreground text-xs">Sem logo</span>
              )}
            </div>
            <div className="flex flex-col gap-2">
              <input
                ref={logoInputRef}
                type="file"
                accept="image/png,image/jpeg,image/webp"
                onChange={handleLogoFileChange}
                className="hidden"
              />
              <div className="flex gap-2">
                <Button type="button" variant="outline" onClick={() => logoInputRef.current?.click()}>
                  Escolher arquivo
                </Button>
                {selectedLogoFile && (
                  <Button type="button" onClick={handleLogoUpload} disabled={isUploadingLogo}>
                    {isUploadingLogo ? "Enviando..." : "Enviar"}
                  </Button>
                )}
              </div>
              <p className="text-muted-foreground text-xs">PNG, JPEG ou WEBP, ate 2MB.</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Banner</CardTitle>
          <CardDescription>Imagem de capa exibida no topo da pagina publica do seu estabelecimento.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-4">
            <div className="bg-muted flex h-32 w-full items-center justify-center overflow-hidden rounded-lg border sm:h-40">
              {bannerPreviewUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- preview de upload local/URL dinamica da API, nao um asset estatico do build.
                <img src={bannerPreviewUrl} alt="Banner do estabelecimento" className="size-full object-cover" />
              ) : (
                <span className="text-muted-foreground text-xs">Sem banner</span>
              )}
            </div>
            <div className="flex flex-col gap-2">
              <input
                ref={bannerInputRef}
                type="file"
                accept="image/png,image/jpeg,image/webp"
                onChange={handleBannerFileChange}
                className="hidden"
              />
              <div className="flex gap-2">
                <Button type="button" variant="outline" onClick={() => bannerInputRef.current?.click()}>
                  Escolher arquivo
                </Button>
                {selectedBannerFile && (
                  <Button type="button" onClick={handleBannerUpload} disabled={isUploadingBanner}>
                    {isUploadingBanner ? "Enviando..." : "Enviar"}
                  </Button>
                )}
              </div>
              <p className="text-muted-foreground text-xs">PNG, JPEG ou WEBP, ate 4MB.</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Cor de marca</CardTitle>
          <CardDescription>Usada em botoes e destaques no painel e no portal publico.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="flex items-center gap-3">
            <input
              type="color"
              value={color}
              onChange={(event) => setColor(event.target.value.toUpperCase())}
              className="h-10 w-14 cursor-pointer rounded-md border border-input"
              aria-label="Selecionar cor de marca"
            />
            <div className="grid gap-1">
              <Label htmlFor="color-hex">Codigo da cor</Label>
              <input
                id="color-hex"
                value={color}
                onChange={(event) => setColor(event.target.value.toUpperCase())}
                maxLength={7}
                className="border-input bg-background h-8 w-28 rounded-md border px-2 font-mono text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
              />
            </div>
          </div>

          <div
            className="flex h-16 items-center justify-center rounded-lg text-sm font-medium"
            style={{ backgroundColor: color, color: FOREGROUND_HEX }}
          >
            Assim vai ficar um botao no seu painel
          </div>

          <p
            className={passesAa ? "text-sm text-emerald-700 dark:text-emerald-400" : "text-destructive text-sm"}
            role="status"
          >
            Contraste com o texto branco: {ratio.toFixed(1)}:1 —{" "}
            {passesAa ? "atende ao padrao de acessibilidade (AA)." : "insuficiente. Escolha um tom mais escuro."}
          </p>

          <Button onClick={handleSaveColor} disabled={isSavingColor || !passesAa} className="mt-2 w-fit">
            {isSavingColor ? "Salvando..." : "Salvar cor de marca"}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Personalizar pagina</CardTitle>
          <CardDescription>Cor de apoio, fonte, estilo de botao e o que aparece na sua pagina publica.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="flex items-center gap-3">
            <input
              type="color"
              value={hasSecondaryColor ? secondaryColor : "#64748B"}
              onChange={(event) => setSecondaryColor(event.target.value.toUpperCase())}
              className="h-10 w-14 cursor-pointer rounded-md border border-input"
              aria-label="Selecionar cor secundaria"
            />
            <div className="grid gap-1">
              <Label htmlFor="secondary-color-hex">Cor secundaria (opcional)</Label>
              <div className="flex items-center gap-2">
                <input
                  id="secondary-color-hex"
                  value={secondaryColor}
                  onChange={(event) => setSecondaryColor(event.target.value.toUpperCase())}
                  placeholder="Sem cor de apoio"
                  maxLength={7}
                  className="border-input bg-background h-8 w-32 rounded-md border px-2 font-mono text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
                />
                {hasSecondaryColor && (
                  <Button type="button" variant="ghost" size="sm" onClick={() => setSecondaryColor("")}>
                    Remover
                  </Button>
                )}
              </div>
            </div>
          </div>

          {hasSecondaryColor && (
            <p
              className={secondaryPassesAa ? "text-sm text-emerald-700 dark:text-emerald-400" : "text-destructive text-sm"}
              role="status"
            >
              Contraste com o texto branco: {secondaryRatio!.toFixed(1)}:1 —{" "}
              {secondaryPassesAa ? "atende ao padrao de acessibilidade (AA)." : "insuficiente. Escolha um tom mais escuro."}
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1">
              <Label>Fonte</Label>
              <Select value={font} onValueChange={(value) => setFont(value as PublicPageFont)}>
                <SelectTrigger className="w-full">
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
            <div className="grid gap-1">
              <Label>Estilo de botao</Label>
              <Select value={buttonStyle} onValueChange={(value) => setButtonStyle(value as PublicPageButtonStyle)}>
                <SelectTrigger className="w-full">
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

          <div className="grid gap-3">
            <Label>O que aparece na pagina publica</Label>
            <div className="flex items-center justify-between gap-4">
              <span className="text-sm">Sobre (descricao no topo)</span>
              <Switch checked={showAboutSection} onCheckedChange={setShowAboutSection} aria-label="Mostrar secao Sobre" />
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-sm">Servicos</span>
              <Switch checked={showServicesSection} onCheckedChange={setShowServicesSection} aria-label="Mostrar secao Servicos" />
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-sm">Equipe</span>
              <Switch checked={showTeamSection} onCheckedChange={setShowTeamSection} aria-label="Mostrar secao Equipe" />
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-sm">Horario de funcionamento</span>
              <Switch checked={showHoursSection} onCheckedChange={setShowHoursSection} aria-label="Mostrar secao Horario" />
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-sm">Contato e redes sociais</span>
              <Switch checked={showContactSection} onCheckedChange={setShowContactSection} aria-label="Mostrar secao Contato" />
            </div>
          </div>

          <div className="mt-2 flex flex-wrap gap-2">
            <Button
              onClick={handleSaveCustomization}
              disabled={isSavingCustomization || !secondaryPassesAa}
              className="w-fit"
            >
              {isSavingCustomization ? "Salvando..." : "Salvar personalizacao"}
            </Button>
            <Button type="button" variant="outline" onClick={handlePreview} className="w-fit">
              Visualizar antes de salvar
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Perfil e contato</CardTitle>
          <CardDescription>Aparece no portal publico do seu estabelecimento (Fase 2).</CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...profileForm}>
            <form onSubmit={profileForm.handleSubmit((values) => profileMutation.mutate(values))} className="grid gap-4">
              <FormField
                control={profileForm.control}
                name="description"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Descricao</FormLabel>
                    <FormControl>
                      <Textarea rows={3} placeholder="Conte um pouco sobre o seu negocio." {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid grid-cols-2 gap-4">
                <FormField
                  control={profileForm.control}
                  name="phone"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Telefone</FormLabel>
                      <FormControl>
                        <Input placeholder="(11) 99999-8888" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={profileForm.control}
                  name="whatsApp"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>WhatsApp</FormLabel>
                      <FormControl>
                        <Input placeholder="(11) 99999-8888" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={profileForm.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>E-mail de contato</FormLabel>
                    <FormControl>
                      <Input type="email" placeholder="contato@seunegocio.com" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={profileForm.control}
                name="address"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Endereco</FormLabel>
                    <FormControl>
                      <Input placeholder="Rua das Flores, 100 - Centro" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid grid-cols-2 gap-4">
                <FormField
                  control={profileForm.control}
                  name="instagramUrl"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Instagram</FormLabel>
                      <FormControl>
                        <Input placeholder="https://instagram.com/seunegocio" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={profileForm.control}
                  name="facebookUrl"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Facebook</FormLabel>
                      <FormControl>
                        <Input placeholder="https://facebook.com/seunegocio" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <Button type="submit" disabled={profileMutation.isPending} className="w-fit">
                {profileMutation.isPending ? "Salvando..." : "Salvar perfil"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Horario de funcionamento</CardTitle>
          <CardDescription>Usado no portal publico para mostrar quando o estabelecimento esta aberto (Fase 2).</CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...hoursForm}>
            <form onSubmit={hoursForm.handleSubmit((values) => businessHoursMutation.mutate(values))} className="flex flex-col gap-3">
              {hoursFieldArray.fields.length === 0 && (
                <p className="text-muted-foreground text-sm">Nenhum horario cadastrado ainda.</p>
              )}
              {hoursFieldArray.fields.map((field, index) => (
                <div key={field.id} className="flex items-end gap-2">
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.dayOfWeek`}
                    render={({ field: dayField }) => (
                      <FormItem className="flex-1">
                        {index === 0 && <FormLabel>Dia</FormLabel>}
                        <Select value={dayField.value} onValueChange={dayField.onChange}>
                          <FormControl>
                            <SelectTrigger className="w-full">
                              <SelectValue />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            {DAY_OPTIONS.map((day) => (
                              <SelectItem key={day.value} value={day.value}>
                                {day.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.startTime`}
                    render={({ field: startField }) => (
                      <FormItem>
                        {index === 0 && <FormLabel>Inicio</FormLabel>}
                        <FormControl>
                          <Input type="time" {...startField} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.endTime`}
                    render={({ field: endField }) => (
                      <FormItem>
                        {index === 0 && <FormLabel>Fim</FormLabel>}
                        <FormControl>
                          <Input type="time" {...endField} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label="Remover horario"
                    onClick={() => hoursFieldArray.remove(index)}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              ))}
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="self-start"
                onClick={() => hoursFieldArray.append({ dayOfWeek: "Monday", startTime: "09:00", endTime: "18:00" })}
              >
                <Plus className="size-4" />
                Adicionar horario
              </Button>
              <Button type="submit" disabled={businessHoursMutation.isPending} className="w-fit">
                {businessHoursMutation.isPending ? "Salvando..." : "Salvar horarios"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Datas fechadas e intervalo entre horarios</CardTitle>
          <CardDescription>
            Feriados e eventos em que o estabelecimento nao atende, e um intervalo minimo de descanso entre agendamentos (Fase 4).
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...schedulingSettingsForm}>
            <form
              onSubmit={schedulingSettingsForm.handleSubmit((values) => schedulingSettingsMutation.mutate(values))}
              className="flex flex-col gap-6"
            >
              <div className="flex flex-col gap-3">
                <Label>Datas fechadas (feriados e eventos)</Label>
                {closedDatesFieldArray.fields.length === 0 && (
                  <p className="text-muted-foreground text-sm">Nenhuma data fechada cadastrada ainda.</p>
                )}
                {closedDatesFieldArray.fields.map((field, index) => (
                  <div key={field.id} className="flex items-end gap-2">
                    <FormField
                      control={schedulingSettingsForm.control}
                      name={`closedDates.${index}.date`}
                      render={({ field: dateField }) => (
                        <FormItem>
                          {index === 0 && <FormLabel>Data</FormLabel>}
                          <FormControl>
                            <Input type="date" {...dateField} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={schedulingSettingsForm.control}
                      name={`closedDates.${index}.reason`}
                      render={({ field: reasonField }) => (
                        <FormItem className="flex-1">
                          {index === 0 && <FormLabel>Motivo (opcional)</FormLabel>}
                          <FormControl>
                            <Input placeholder="Ex.: Natal, reforma" {...reasonField} />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label="Remover data fechada"
                      onClick={() => closedDatesFieldArray.remove(index)}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  </div>
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="self-start"
                  onClick={() => closedDatesFieldArray.append({ date: "", reason: "" })}
                >
                  <Plus className="size-4" />
                  Adicionar data fechada
                </Button>
              </div>

              <FormField
                control={schedulingSettingsForm.control}
                name="appointmentBufferMinutes"
                render={({ field: bufferField }) => (
                  <FormItem className="max-w-xs">
                    <FormLabel>Intervalo minimo entre agendamentos (minutos)</FormLabel>
                    <FormControl>
                      <Input type="number" min={0} max={240} {...bufferField} value={bufferField.value as number} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <Button type="submit" disabled={schedulingSettingsMutation.isPending} className="w-fit">
                {schedulingSettingsMutation.isPending ? "Salvando..." : "Salvar configuracoes de agendamento"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>
    </>
  );
}
