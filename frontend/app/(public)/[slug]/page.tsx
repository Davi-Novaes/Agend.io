import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Inter, Lora, Merriweather, Montserrat, Playfair_Display, Poppins } from "next/font/google";
import { ArrowRight, CalendarCheck, Camera, CheckCircle2, Clock3, ExternalLink, MapPin, MessageCircle, Phone, ShieldCheck, Sparkles, Star, User } from "lucide-react";

import { getTenantBySlug, publicListServices, publicListResources, resolveAssetUrl, ApiError, type TenantPublicProfile, type PublicServiceSummary, type PublicResourceSummary, type PublicPageFont, type PublicPageButtonStyle, type WorkingHourEntry } from "@/lib/api/client";
import { TenantThemeProvider } from "@/lib/tenant/tenant-theme-provider";
import { DEFAULT_TENANT_THEME } from "@/lib/tenant/tenant-theme";
import { BookingFlow } from "@/components/public/booking-flow";
import { LoyaltyLookup } from "@/components/public/loyalty-lookup";
import { PortalAccountMenu } from "@/components/public/portal-account-menu";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";

type PageProps = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ preview?: string; servico?: string }>;
};

const poppins = Poppins({ weight: ["400", "600", "700"], subsets: ["latin"], variable: "--font-tenant-poppins" });
const playfairDisplay = Playfair_Display({ subsets: ["latin"], variable: "--font-tenant-playfair" });
const merriweather = Merriweather({ weight: ["400", "700"], subsets: ["latin"], variable: "--font-tenant-merriweather" });
const inter = Inter({ weight: ["400", "600", "700"], subsets: ["latin"], variable: "--font-tenant-inter" });
const montserrat = Montserrat({ weight: ["400", "600", "700"], subsets: ["latin"], variable: "--font-tenant-montserrat" });
const lora = Lora({ weight: ["400", "700"], subsets: ["latin"], variable: "--font-tenant-lora" });

const FONT_CLASS_NAME: Record<PublicPageFont, string> = {
  Default: "",
  Poppins: poppins.className,
  PlayfairDisplay: playfairDisplay.className,
  Merriweather: merriweather.className,
  Inter: inter.className,
  Montserrat: montserrat.className,
  Lora: lora.className,
};

const BUTTON_RADIUS: Record<PublicPageButtonStyle, string> = {
  Rounded: "var(--radius-lg)",
  Square: "0.125rem",
  Pill: "9999px",
};

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
  if (!encoded) return null;
  try {
    return JSON.parse(Buffer.from(encoded, "base64").toString("utf-8")) as PreviewOverride;
  } catch {
    return null;
  }
}

const DAY_ORDER: WorkingHourEntry["dayOfWeek"][] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
const DAY_LABELS: Record<WorkingHourEntry["dayOfWeek"], string> = {
  Monday: "Segunda-feira", Tuesday: "Terça-feira", Wednesday: "Quarta-feira", Thursday: "Quinta-feira",
  Friday: "Sexta-feira", Saturday: "Sábado", Sunday: "Domingo",
};

async function loadTenant(slug: string): Promise<TenantPublicProfile | null> {
  try {
    return await getTenantBySlug(slug);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const tenant = await loadTenant(slug);
  return {
    title: tenant ? `${tenant.name} | Agendamento online` : "Estabelecimento não encontrado",
    description: tenant ? (tenant.description ?? `Conheça os serviços e agende seu horário em ${tenant.name}.`) : undefined,
  };
}

function formatPrice(amount: number, currency: string): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency }).format(amount);
}

function whatsAppLink(whatsApp: string): string {
  const digits = whatsApp.replace(/\D/g, "");
  return `https://wa.me/${digits}?text=${encodeURIComponent("Olá! Vim pelo site e gostaria de agendar um horário.")}`;
}

function SectionHeading({ eyebrow, title, description }: { eyebrow: string; title: string; description?: string }) {
  return (
    <div className="mx-auto max-w-2xl text-center">
      <p className="text-primary text-xs font-bold tracking-[0.18em] uppercase">{eyebrow}</p>
      <h2 className="mt-2 text-2xl font-semibold tracking-tight text-balance sm:text-4xl">{title}</h2>
      {description && <p className="text-muted-foreground mt-3 text-sm leading-relaxed text-balance sm:text-base">{description}</p>}
    </div>
  );
}

export default async function TenantPortalPage({ params, searchParams }: PageProps) {
  const { slug } = await params;
  const { preview, servico } = await searchParams;
  const tenant = await loadTenant(slug);
  if (!tenant || !tenant.isActive || !tenant.publicPageEnabled) notFound();

  const [services, resources] = await Promise.all([
    publicListServices(tenant.id).catch(() => [] as PublicServiceSummary[]),
    publicListResources(tenant.id).catch(() => [] as PublicResourceSummary[]),
  ]);

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
  const orderedBusinessHours = [...tenant.businessHours].sort((a, b) => DAY_ORDER.indexOf(a.dayOfWeek) - DAY_ORDER.indexOf(b.dayOfWeek));
  const hasContactInfo = Boolean(tenant.address || tenant.phone || tenant.whatsApp || tenant.instagramUrl || tenant.facebookUrl);
  const theme = {
    ...(tenant.primaryColorHex ? { primary: tenant.primaryColorHex, primaryForeground: "#ffffff" } : DEFAULT_TENANT_THEME),
    ...(customization.secondaryColorHex ? { secondary: customization.secondaryColorHex, secondaryForeground: "#ffffff" } : {}),
    buttonRadius: BUTTON_RADIUS[customization.buttonStyle],
  };
  const buttonRadiusClassName = "rounded-[var(--tenant-button-radius)]";
  const hasDiscoveryNavigation = customization.showServicesSection || customization.showTeamSection || customization.showHoursSection;

  return (
    <TenantThemeProvider theme={theme}>
      <div className={`min-h-full bg-background text-foreground ${FONT_CLASS_NAME[customization.font]}`}>
        <header className="bg-background/92 sticky top-0 z-50 border-b backdrop-blur-xl">
          <div className="mx-auto flex h-16 w-full max-w-6xl items-center gap-4 px-4 sm:px-6">
            <Link href={`/${slug}`} className="flex min-w-0 items-center gap-2.5" aria-label={`Início — ${tenant.name}`}>
              {tenant.logoUrl ? (
                <Image src={resolveAssetUrl(tenant.logoUrl)} alt="" width={36} height={36} className="size-9 rounded-xl bg-card object-contain p-0.5 ring-1 ring-border" unoptimized />
              ) : (
                <span className="bg-primary text-primary-foreground flex size-9 items-center justify-center rounded-xl"><Sparkles className="size-4" /></span>
              )}
              <span className="truncate font-semibold tracking-tight">{tenant.name}</span>
            </Link>
            {hasDiscoveryNavigation && (
              <nav className="ml-auto hidden items-center gap-6 text-sm md:flex" aria-label="Navegação da página">
                {customization.showServicesSection && services.length > 0 && <Link href="#servicos" className="text-muted-foreground hover:text-foreground">Serviços</Link>}
                {customization.showTeamSection && resources.length > 0 && <Link href="#equipe" className="text-muted-foreground hover:text-foreground">Equipe</Link>}
                {customization.showHoursSection && orderedBusinessHours.length > 0 && <Link href="#horarios" className="text-muted-foreground hover:text-foreground">Horários</Link>}
              </nav>
            )}
            <PortalAccountMenu
              tenantId={tenant.id}
              slug={slug}
              className={`${hasDiscoveryNavigation ? "" : "ml-auto"} ${buttonRadiusClassName}`}
            />
            <Button className={buttonRadiusClassName} asChild>
              <Link href="#agendar">Agendar <CalendarCheck className="size-4" /></Link>
            </Button>
          </div>
        </header>

        <main>
          <section className="relative isolate overflow-hidden bg-primary">
            {tenant.bannerUrl && (
              <><Image src={resolveAssetUrl(tenant.bannerUrl)} alt="" fill sizes="100vw" className="object-cover" unoptimized priority /><div aria-hidden className="absolute inset-0 bg-gradient-to-r from-black/85 via-black/60 to-black/25" /></>
            )}
            {!tenant.bannerUrl && (
              <div aria-hidden className="absolute inset-0 overflow-hidden">
                <div className="absolute -top-32 -right-20 size-96 rounded-full bg-white/10 blur-3xl" />
                <div className="absolute -bottom-40 left-1/4 size-96 rounded-full bg-black/15 blur-3xl" />
                <div className="absolute inset-0 opacity-15 [background-image:linear-gradient(115deg,transparent_0%,transparent_49%,white_50%,transparent_51%,transparent_100%)] [background-size:76px_76px]" />
              </div>
            )}
            <div className="relative mx-auto grid min-h-[540px] w-full max-w-6xl items-center px-4 py-20 sm:px-6 lg:grid-cols-[1fr_360px] lg:gap-16 lg:py-24">
              <div className="max-w-3xl text-white">
                <div className="mb-6 flex items-center gap-3">
                  {tenant.logoUrl && <Image src={resolveAssetUrl(tenant.logoUrl)} alt={`Logo de ${tenant.name}`} width={64} height={64} className="size-16 rounded-2xl bg-white object-contain p-1.5 shadow-xl" unoptimized />}
                  <div><p className="text-sm font-semibold text-white/90">{tenant.name}</p><p className="mt-0.5 flex items-center gap-1.5 text-xs text-white/70"><CheckCircle2 className="size-3.5" /> Agendamento online</p></div>
                </div>
                <h1 className="max-w-3xl text-4xl leading-[1.08] font-bold tracking-tight text-balance drop-shadow-sm sm:text-5xl lg:text-6xl">{tenant.homeHeroTitle || "Seu próximo horário começa aqui."}</h1>
                {customization.showAboutSection && (tenant.homeHeroDescription || tenant.description) && <p className="mt-5 max-w-2xl text-base leading-relaxed text-white/85 text-balance sm:text-lg">{tenant.homeHeroDescription || tenant.description}</p>}
                <div className="mt-8 flex flex-wrap gap-3">
                  <Button size="lg" className={`h-11 bg-white px-5 text-primary shadow-lg hover:bg-white/90 ${buttonRadiusClassName}`} asChild><Link href="#agendar">{tenant.homeCtaText || "Agendar agora"}<ArrowRight className="size-4" /></Link></Button>
                  {tenant.whatsApp && <Button size="lg" variant="outline" className={`h-11 border-white/35 bg-white/10 px-5 text-white backdrop-blur-sm hover:bg-white/20 hover:text-white ${buttonRadiusClassName}`} asChild><a href={whatsAppLink(tenant.whatsApp)} target="_blank" rel="noopener noreferrer"><MessageCircle className="size-4" />Falar conosco</a></Button>}
                </div>
              </div>
              <div className="mt-12 hidden rounded-3xl border border-white/20 bg-black/20 p-5 text-white shadow-2xl backdrop-blur-md lg:block">
                <p className="text-xs font-semibold tracking-wider text-white/65 uppercase">Agende em poucos passos</p>
                <div className="mt-5 space-y-4">
                  {[["1", "Escolha o serviço", "Encontre a opção ideal para você"], ["2", "Selecione o horário", "Veja a agenda em tempo real"], ["3", "Confirme seus dados", "Pronto, sem ligações ou espera"]].map(([number, title, text]) => (
                    <div key={number} className="flex gap-3"><span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-white text-xs font-bold text-primary">{number}</span><div><p className="text-sm font-semibold">{title}</p><p className="mt-0.5 text-xs text-white/65">{text}</p></div></div>
                  ))}
                </div>
              </div>
            </div>
          </section>

          <section aria-label="Vantagens do agendamento" className="relative z-10 mx-auto -mt-7 w-[calc(100%-2rem)] max-w-5xl rounded-2xl border bg-card px-4 py-4 shadow-xl sm:px-6">
            <div className="grid gap-4 sm:grid-cols-3 sm:divide-x">
              <div className="flex items-center gap-3 sm:px-4"><Clock3 className="text-primary size-5 shrink-0" /><div><p className="text-sm font-semibold">Agende a qualquer hora</p><p className="text-muted-foreground text-xs">Disponível 24 horas por dia</p></div></div>
              <div className="flex items-center gap-3 sm:px-4"><CalendarCheck className="text-primary size-5 shrink-0" /><div><p className="text-sm font-semibold">Horários atualizados</p><p className="text-muted-foreground text-xs">Escolha uma vaga disponível</p></div></div>
              <div className="flex items-center gap-3 sm:px-4"><ShieldCheck className="text-primary size-5 shrink-0" /><div><p className="text-sm font-semibold">Reserva segura</p><p className="text-muted-foreground text-xs">Confirmação rápida e prática</p></div></div>
            </div>
          </section>

          <div className="mx-auto flex w-full max-w-6xl flex-col gap-24 px-4 py-20 sm:px-6 sm:py-28">
            {customization.showServicesSection && services.length > 0 && (
              <section id="servicos" aria-labelledby="servicos-heading" className="scroll-mt-24">
                <SectionHeading eyebrow="Serviços" title="Escolha o cuidado que combina com você" description="Conheça nossas opções, valores e duração antes de reservar." />
                <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                  {services.map((service) => (
                    <Card key={service.id} className="group/service gap-0 overflow-hidden py-0 transition-all hover:-translate-y-1 hover:shadow-xl">
                      <div className="relative aspect-[16/10] overflow-hidden bg-primary/8">
                        {service.imageUrl ? <Image src={resolveAssetUrl(service.imageUrl)} alt="" fill sizes="(max-width: 640px) 100vw, 33vw" className="object-cover transition-transform duration-500 group-hover/service:scale-105" unoptimized /> : <div className="flex size-full items-center justify-center"><Sparkles className="text-primary size-10" strokeWidth={1.25} /></div>}
                        {service.category && <Badge className="absolute top-3 left-3 border-0 bg-background/90 text-foreground shadow-sm backdrop-blur">{service.category}</Badge>}
                      </div>
                      <CardContent className="flex flex-1 flex-col p-5">
                        <h3 className="text-lg font-semibold">{service.name}</h3>
                        {service.description && <p className="text-muted-foreground mt-2 line-clamp-2 text-sm leading-relaxed">{service.description}</p>}
                        <div className="mt-5 flex items-center gap-3 border-t pt-4"><span className="text-muted-foreground flex items-center gap-1.5 text-xs"><Clock3 className="size-3.5" />{service.durationMinutes} min</span><span className="ml-auto font-semibold">{formatPrice(service.price, service.currency)}</span></div>
                        <Button variant="outline" className={`mt-4 w-full group-hover/service:border-primary group-hover/service:text-primary ${buttonRadiusClassName}`} asChild><Link href={`/${slug}?servico=${encodeURIComponent(service.id)}#agendar`}>Agendar este serviço <ArrowRight className="size-4" /></Link></Button>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              </section>
            )}

            {customization.showTeamSection && resources.length > 0 && (
              <section id="equipe" aria-labelledby="equipe-heading" className="scroll-mt-24">
                <SectionHeading eyebrow="Nossa equipe" title="Profissionais que cuidam de cada detalhe" description="Escolha com quem você quer ser atendido durante o agendamento." />
                <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {resources.map((resource) => (
                    <Card key={resource.id} className="transition-shadow hover:shadow-lg"><CardContent className="flex items-center gap-4 p-5">
                      <Avatar size="lg" className="size-16 shrink-0 ring-4 ring-primary/10">{resource.photoUrl && <AvatarImage src={resolveAssetUrl(resource.photoUrl)} alt="" />}<AvatarFallback className="bg-primary/10 text-primary"><User className="size-6" strokeWidth={1.5} /></AvatarFallback></Avatar>
                      <div className="min-w-0"><h3 className="font-semibold">{resource.name}</h3>{resource.specialties.length > 0 && <p className="text-primary mt-1 text-xs font-medium">{resource.specialties.join(" · ")}</p>}{resource.description && <p className="text-muted-foreground mt-2 line-clamp-2 text-xs leading-relaxed">{resource.description}</p>}</div>
                    </CardContent></Card>
                  ))}
                </div>
              </section>
            )}

            {customization.showHoursSection && orderedBusinessHours.length > 0 && (
              <section id="horarios" aria-labelledby="horarios-heading" className="scroll-mt-24">
                <div className="grid items-center gap-10 lg:grid-cols-[0.9fr_1.1fr]">
                  <div className="lg:pr-10"><p className="text-primary text-xs font-bold tracking-[0.18em] uppercase">Planeje sua visita</p><h2 id="horarios-heading" className="mt-2 text-3xl font-semibold tracking-tight text-balance sm:text-4xl">Estamos esperando por você</h2><p className="text-muted-foreground mt-4 leading-relaxed">Consulte os horários de funcionamento e reserve o melhor momento pela agenda online.</p>{tenant.address && <p className="mt-6 flex items-start gap-2 text-sm"><MapPin className="text-primary mt-0.5 size-4 shrink-0" />{tenant.address}</p>}<Button className={`mt-6 ${buttonRadiusClassName}`} asChild><Link href="#agendar">Ver horários disponíveis</Link></Button></div>
                  <Card className="shadow-lg"><CardContent className="divide-y p-2 sm:p-4">{orderedBusinessHours.map((entry) => <div key={entry.dayOfWeek} className="flex items-center justify-between gap-4 px-3 py-3 text-sm"><span className="font-medium">{DAY_LABELS[entry.dayOfWeek]}</span><span className="text-muted-foreground flex items-center gap-2"><Clock3 className="size-3.5" />{entry.startTime.slice(0, 5)} às {entry.endTime.slice(0, 5)}</span></div>)}</CardContent></Card>
                </div>
              </section>
            )}
          </div>

          <section id="agendar" aria-labelledby="agendar-heading" className="relative scroll-mt-16 overflow-hidden bg-primary/6 px-4 py-20 sm:px-6 sm:py-28">
            <div aria-hidden className="absolute inset-0 opacity-30 [background-image:radial-gradient(circle_at_center,var(--primary)_1px,transparent_1px)] [background-size:28px_28px]" />
            <div className="relative mx-auto max-w-5xl">
              <SectionHeading eyebrow="Agendamento online" title="Reserve seu momento" description={tenant.bookingInstructionsText || "Escolha o serviço, o profissional e o melhor horário. Leva só alguns minutos."} />
              <Card className="mt-10 border-primary/15 shadow-2xl"><CardContent className="p-5 sm:p-8 lg:p-10"><BookingFlow key={servico ?? "new-booking"} tenantId={tenant.id} buttonRadiusClassName={buttonRadiusClassName} paymentRequired={tenant.paymentRequired} depositPercentage={tenant.depositPercentage} initialServices={services} initialResources={resources} initialServiceId={servico} customerPortalHref={`/${slug}/minha-conta`} /></CardContent></Card>
            </div>
          </section>

          <section aria-labelledby="fidelidade-heading" className="mx-auto w-full max-w-5xl px-4 py-20 sm:px-6">
            <Card className="overflow-hidden border-primary/15 bg-primary text-primary-foreground shadow-xl"><CardContent className="grid gap-8 p-6 sm:p-8 lg:grid-cols-[0.8fr_1.2fr] lg:items-center">
              <div><div className="mb-4 flex size-11 items-center justify-center rounded-xl bg-white/15"><Star className="size-5" /></div><p className="text-xs font-bold tracking-[0.18em] text-white/70 uppercase">Clube de vantagens</p><h2 id="fidelidade-heading" className="mt-2 text-2xl font-semibold">Consulte seus pontos</h2><p className="mt-2 text-sm leading-relaxed text-white/75">Já é cliente? Veja seu saldo e acompanhe seus benefícios.</p></div>
              <div className="rounded-2xl bg-background p-5 text-foreground shadow-inner sm:p-6"><LoyaltyLookup tenantId={tenant.id} buttonRadiusClassName={buttonRadiusClassName} /></div>
            </CardContent></Card>
          </section>
        </main>

        <footer className="border-t bg-card">
          <div className="mx-auto grid w-full max-w-6xl gap-8 px-4 py-12 sm:px-6 md:grid-cols-[1fr_auto] md:items-end">
            <div><div className="flex items-center gap-3">{tenant.logoUrl ? <Image src={resolveAssetUrl(tenant.logoUrl)} alt="" width={42} height={42} className="size-10 rounded-xl object-contain ring-1 ring-border" unoptimized /> : <span className="bg-primary text-primary-foreground flex size-10 items-center justify-center rounded-xl"><Sparkles className="size-4" /></span>}<div><p className="font-semibold">{tenant.name}</p><p className="text-muted-foreground text-xs">Agendamento online fácil e seguro</p></div></div>
              {customization.showContactSection && hasContactInfo && <div className="text-muted-foreground mt-6 flex flex-wrap gap-x-5 gap-y-3 text-sm">{tenant.address && <span className="flex items-center gap-1.5"><MapPin className="size-4" />{tenant.address}</span>}{tenant.phone && <a className="hover:text-foreground flex items-center gap-1.5" href={`tel:${tenant.phone}`}><Phone className="size-4" />{tenant.phone}</a>}{tenant.whatsApp && <a className="hover:text-foreground flex items-center gap-1.5" href={whatsAppLink(tenant.whatsApp)} target="_blank" rel="noopener noreferrer"><MessageCircle className="size-4" />WhatsApp</a>}</div>}
            </div>
            {customization.showContactSection && (tenant.instagramUrl || tenant.facebookUrl) && <div className="flex gap-2">{tenant.instagramUrl && <Button variant="outline" size="icon" aria-label="Instagram" asChild><a href={tenant.instagramUrl} target="_blank" rel="noopener noreferrer"><Camera className="size-4" /></a></Button>}{tenant.facebookUrl && <Button variant="outline" size="sm" asChild><a href={tenant.facebookUrl} target="_blank" rel="noopener noreferrer">Facebook <ExternalLink className="size-3.5" /></a></Button>}</div>}
          </div>
          <div className="border-t px-4 py-4 text-center text-xs text-muted-foreground">Agendamento online por AgendioBR</div>
        </footer>
      </div>
    </TenantThemeProvider>
  );
}
