import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Merriweather, Playfair_Display, Poppins } from "next/font/google";
import {
  Clock,
  ExternalLink,
  MapPin,
  MessageCircle,
  Phone,
  Sparkles,
  User,
} from "lucide-react";

import {
  getTenantBySlug,
  publicListServices,
  publicListResources,
  resolveAssetUrl,
  ApiError,
  type TenantPublicProfile,
  type PublicServiceSummary,
  type PublicResourceSummary,
  type PublicPageFont,
  type PublicPageButtonStyle,
  type WorkingHourEntry,
} from "@/lib/api/client";
import { TenantThemeProvider } from "@/lib/tenant/tenant-theme-provider";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { BookingFlow } from "@/components/public/booking-flow";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";

type PageProps = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ preview?: string }>;
};

// Lista curada de fontes (Fase 3 — Personalizacao da pagina). Aplicada so
// nesta rota publica, nunca no painel autenticado.
const poppins = Poppins({ weight: ["400", "600", "700"], subsets: ["latin"], variable: "--font-tenant-poppins" });
const playfairDisplay = Playfair_Display({ subsets: ["latin"], variable: "--font-tenant-playfair" });
const merriweather = Merriweather({ weight: ["400", "700"], subsets: ["latin"], variable: "--font-tenant-merriweather" });

const FONT_CLASS_NAME: Record<PublicPageFont, string> = {
  Default: "",
  Poppins: poppins.className,
  PlayfairDisplay: playfairDisplay.className,
  Merriweather: merriweather.className,
};

const BUTTON_RADIUS: Record<PublicPageButtonStyle, string> = {
  Rounded: "var(--radius-lg)",
  Square: "0.125rem",
  Pill: "9999px",
};

// Campos que o dono pode pre-visualizar antes de salvar (settings/branding)
// — ver PageCustomizationOverride no card "Personalizar pagina". Conteudo
// (descricao, servicos, etc.) sempre vem do dado real ja salvo.
type PreviewOverride = {
  secondaryColorHex: string | null;
  font: PublicPageFont;
  buttonStyle: PublicPageButtonStyle;
  showAboutSection: boolean;
  showServicesSection: boolean;
  showTeamSection: boolean;
  showHoursSection: boolean;
  showContactSection: boolean;
};

function decodePreviewOverride(encoded: string | undefined): PreviewOverride | null {
  if (!encoded) {
    return null;
  }

  try {
    const json = Buffer.from(encoded, "base64").toString("utf-8");
    return JSON.parse(json) as PreviewOverride;
  } catch {
    return null;
  }
}

const DAY_ORDER: WorkingHourEntry["dayOfWeek"][] = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

const DAY_LABELS: Record<WorkingHourEntry["dayOfWeek"], string> = {
  Monday: "Segunda-feira",
  Tuesday: "Terca-feira",
  Wednesday: "Quarta-feira",
  Thursday: "Quinta-feira",
  Friday: "Sexta-feira",
  Saturday: "Sabado",
  Sunday: "Domingo",
};

async function loadTenant(slug: string): Promise<TenantPublicProfile | null> {
  try {
    return await getTenantBySlug(slug);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }
    throw error;
  }
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const tenant = await loadTenant(slug);

  return {
    title: tenant ? tenant.name : "Estabelecimento nao encontrado",
    description: tenant
      ? (tenant.description ?? `Agende um horario em ${tenant.name} pela internet, em poucos passos.`)
      : undefined,
  };
}

function formatPrice(amount: number, currency: string): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency }).format(amount);
}

function whatsAppLink(whatsApp: string): string {
  const digits = whatsApp.replace(/\D/g, "");
  return `https://wa.me/${digits}?text=${encodeURIComponent("Ola! Vim pelo site e gostaria de agendar um horario.")}`;
}

// SSR de proposito (nao 'use client'): esta e a pagina publica indexavel do
// tenant — precisa de HTML pronto no primeiro request para SEO. A parte
// interativa (fluxo de agendamento) vive num Client Component a parte.
export default async function TenantPortalPage({ params, searchParams }: PageProps) {
  const { slug } = await params;
  const { preview } = await searchParams;
  const tenant = await loadTenant(slug);

  if (!tenant || !tenant.isActive) {
    notFound();
  }

  const [services, resources] = await Promise.all([
    publicListServices(tenant.id).catch(() => [] as PublicServiceSummary[]),
    publicListResources(tenant.id).catch(() => [] as PublicResourceSummary[]),
  ]);

  // Preview antes de salvar (Fase 3): so os campos de personalizacao sao
  // sobrepostos com valores ainda nao salvos — conteudo continua vindo do
  // dado real, coerente com o resto do produto (edicao sempre salva na hora).
  const previewOverride = decodePreviewOverride(preview);
  const customization: PreviewOverride = previewOverride ?? {
    secondaryColorHex: tenant.secondaryColorHex,
    font: tenant.font,
    buttonStyle: tenant.buttonStyle,
    showAboutSection: tenant.showAboutSection,
    showServicesSection: tenant.showServicesSection,
    showTeamSection: tenant.showTeamSection,
    showHoursSection: tenant.showHoursSection,
    showContactSection: tenant.showContactSection,
  };

  const orderedBusinessHours = [...tenant.businessHours].sort(
    (a, b) => DAY_ORDER.indexOf(a.dayOfWeek) - DAY_ORDER.indexOf(b.dayOfWeek)
  );

  const hasContactInfo = Boolean(
    tenant.address || tenant.phone || tenant.whatsApp || tenant.instagramUrl || tenant.facebookUrl
  );

  // A cor customizada so foi aceita ao salvar (settings/branding) se tivesse
  // contraste AA suficiente contra branco — por isso o texto sobre ela pode
  // ser sempre branco aqui, sem recalcular nada.
  const theme = {
    ...(tenant.primaryColorHex
      ? { primary: tenant.primaryColorHex, primaryForeground: "#ffffff" }
      : DEFAULT_TENANT_THEME),
    ...(customization.secondaryColorHex
      ? { secondary: customization.secondaryColorHex, secondaryForeground: "#ffffff" }
      : {}),
    buttonRadius: BUTTON_RADIUS[customization.buttonStyle],
  };

  const buttonRadiusClassName = "rounded-[var(--tenant-button-radius)]";

  return (
    <TenantThemeProvider theme={theme}>
      <div className={`flex min-h-full flex-1 flex-col ${FONT_CLASS_NAME[customization.font]}`}>
        <header className="relative isolate flex min-h-64 flex-col items-center justify-end overflow-hidden bg-primary px-4 py-10 text-center sm:min-h-80 sm:py-14">
          {tenant.bannerUrl && (
            <>
              <Image
                src={resolveAssetUrl(tenant.bannerUrl)}
                alt=""
                fill
                sizes="100vw"
                className="object-cover"
                unoptimized
                priority
              />
              <div aria-hidden className="absolute inset-0 bg-gradient-to-t from-black/75 via-black/35 to-black/10" />
            </>
          )}

          <div className="relative flex flex-col items-center gap-3">
            {tenant.logoUrl ? (
              <Image
                src={resolveAssetUrl(tenant.logoUrl)}
                alt={`Logo de ${tenant.name}`}
                width={80}
                height={80}
                className="rounded-2xl bg-background object-contain p-1.5 shadow-lg"
                unoptimized
              />
            ) : (
              <div className="flex size-20 items-center justify-center rounded-2xl bg-background/90 shadow-lg">
                <Sparkles className="text-primary size-8" strokeWidth={1.5} />
              </div>
            )}
            <h1 className="text-3xl font-semibold tracking-tight text-white text-balance drop-shadow-sm sm:text-4xl">
              {tenant.name}
            </h1>
            {customization.showAboutSection && tenant.description && (
              <p className="max-w-xl text-sm text-white/90 text-balance drop-shadow-sm sm:text-base">
                {tenant.description}
              </p>
            )}
            <Button size="lg" className={`mt-2 shadow-md ${buttonRadiusClassName}`} asChild>
              <Link href="#agendar">Agendar horario</Link>
            </Button>
          </div>
        </header>

        <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-16 px-4 py-14 sm:px-6 sm:py-20">
          {customization.showServicesSection && services.length > 0 && (
            <section aria-labelledby="servicos-heading" className="flex flex-col gap-6">
              <div className="text-center">
                <h2 id="servicos-heading" className="text-2xl font-semibold tracking-tight sm:text-3xl">
                  Nossos servicos
                </h2>
              </div>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {services.map((service) => (
                  <Card key={service.id}>
                    {service.imageUrl ? (
                      <Image
                        src={resolveAssetUrl(service.imageUrl)}
                        alt=""
                        width={400}
                        height={220}
                        className="aspect-video w-full object-cover"
                        unoptimized
                      />
                    ) : (
                      <div className="bg-muted flex aspect-video w-full items-center justify-center">
                        <Sparkles className="text-muted-foreground size-8" strokeWidth={1.5} />
                      </div>
                    )}
                    <CardContent className="flex flex-col gap-1">
                      <div className="flex items-start justify-between gap-2">
                        <p className="font-medium">{service.name}</p>
                        {service.category && (
                          <Badge variant="secondary" className="shrink-0">
                            {service.category}
                          </Badge>
                        )}
                      </div>
                      <p className="text-muted-foreground text-sm">
                        {service.durationMinutes} min &middot; {formatPrice(service.price, service.currency)}
                      </p>
                      {service.description && (
                        <p className="text-muted-foreground mt-1 text-sm">{service.description}</p>
                      )}
                    </CardContent>
                  </Card>
                ))}
              </div>
            </section>
          )}

          {customization.showTeamSection && resources.length > 0 && (
            <section aria-labelledby="profissionais-heading" className="flex flex-col gap-6">
              <div className="text-center">
                <h2 id="profissionais-heading" className="text-2xl font-semibold tracking-tight sm:text-3xl">
                  Nossa equipe
                </h2>
              </div>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {resources.map((resource) => (
                  <Card key={resource.id}>
                    <CardContent className="flex flex-col items-center gap-3 text-center">
                      <Avatar size="lg" className="size-16">
                        {resource.photoUrl && <AvatarImage src={resolveAssetUrl(resource.photoUrl)} alt="" />}
                        <AvatarFallback>
                          <User className="size-6" strokeWidth={1.5} />
                        </AvatarFallback>
                      </Avatar>
                      <p className="font-medium">{resource.name}</p>
                      {resource.specialties.length > 0 && (
                        <div className="flex flex-wrap justify-center gap-1.5">
                          {resource.specialties.map((specialty) => (
                            <Badge key={specialty} variant="outline">
                              {specialty}
                            </Badge>
                          ))}
                        </div>
                      )}
                      {resource.description && (
                        <p className="text-muted-foreground text-sm">{resource.description}</p>
                      )}
                    </CardContent>
                  </Card>
                ))}
              </div>
            </section>
          )}

          {customization.showHoursSection && orderedBusinessHours.length > 0 && (
            <section aria-labelledby="horarios-heading" className="mx-auto flex w-full max-w-md flex-col gap-6">
              <div className="text-center">
                <h2 id="horarios-heading" className="text-2xl font-semibold tracking-tight sm:text-3xl">
                  Horario de funcionamento
                </h2>
              </div>
              <Card>
                <CardContent className="divide-y">
                  {orderedBusinessHours.map((entry) => (
                    <div key={entry.dayOfWeek} className="flex items-center justify-between py-2.5 text-sm first:pt-0 last:pb-0">
                      <span className="flex items-center gap-2 font-medium">
                        <Clock className="text-muted-foreground size-4" />
                        {DAY_LABELS[entry.dayOfWeek]}
                      </span>
                      <span className="text-muted-foreground">
                        {entry.startTime.slice(0, 5)} as {entry.endTime.slice(0, 5)}
                      </span>
                    </div>
                  ))}
                </CardContent>
              </Card>
            </section>
          )}

          <section id="agendar" aria-labelledby="agendar-heading" className="flex flex-col gap-6 scroll-mt-8">
            <div className="text-center">
              <h2 id="agendar-heading" className="text-2xl font-semibold tracking-tight sm:text-3xl">
                Agende seu horario
              </h2>
            </div>
            <Card className="mx-auto w-full max-w-lg">
              <CardContent>
                <BookingFlow tenantId={tenant.id} buttonRadiusClassName={buttonRadiusClassName} />
              </CardContent>
            </Card>
          </section>
        </main>

        {customization.showContactSection && hasContactInfo && (
          <footer className="border-t">
            <div className="mx-auto flex w-full max-w-5xl flex-col items-center gap-4 px-4 py-10 text-center sm:px-6">
              {tenant.address && (
                <p className="text-muted-foreground flex items-center gap-2 text-sm">
                  <MapPin className="size-4 shrink-0" />
                  {tenant.address}
                </p>
              )}
              <div className="flex flex-wrap items-center justify-center gap-3">
                {tenant.phone && (
                  <Button variant="outline" size="sm" className={buttonRadiusClassName} asChild>
                    <a href={`tel:${tenant.phone}`}>
                      <Phone className="size-4" />
                      {tenant.phone}
                    </a>
                  </Button>
                )}
                {tenant.whatsApp && (
                  <Button variant="outline" size="sm" className={buttonRadiusClassName} asChild>
                    <a href={whatsAppLink(tenant.whatsApp)} target="_blank" rel="noopener noreferrer">
                      <MessageCircle className="size-4" />
                      WhatsApp
                    </a>
                  </Button>
                )}
                {tenant.instagramUrl && (
                  <Button variant="outline" size="sm" className={buttonRadiusClassName} asChild>
                    <a href={tenant.instagramUrl} target="_blank" rel="noopener noreferrer">
                      Instagram
                      <ExternalLink className="size-3.5" />
                    </a>
                  </Button>
                )}
                {tenant.facebookUrl && (
                  <Button variant="outline" size="sm" className={buttonRadiusClassName} asChild>
                    <a href={tenant.facebookUrl} target="_blank" rel="noopener noreferrer">
                      Facebook
                      <ExternalLink className="size-3.5" />
                    </a>
                  </Button>
                )}
              </div>
            </div>
          </footer>
        )}
      </div>
    </TenantThemeProvider>
  );
}
