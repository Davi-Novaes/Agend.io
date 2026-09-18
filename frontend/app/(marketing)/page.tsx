import type { Metadata } from "next";
import Link from "next/link";
import {
  Activity,
  Apple,
  Bell,
  Brain,
  Calendar,
  Camera,
  Check,
  CreditCard,
  Droplets,
  Dumbbell,
  HeartPulse,
  Mail,
  MessageCircle,
  Palette,
  PawPrint,
  Scale,
  Scissors,
  ShieldCheck,
  Smile,
  Sparkles,
  Users,
  Wallet,
} from "lucide-react";

import { SiteHeader } from "@/components/marketing/site-header";
import { CursorSpotlight } from "@/components/cursor-spotlight";
import { HeroDashboardMockup } from "@/components/marketing/hero-dashboard-mockup";
import { ProductShowcase } from "@/components/marketing/product-showcase";
import { HowItWorks } from "@/components/marketing/how-it-works";
import { TrustStats } from "@/components/marketing/trust-stats";
import { ScrollReveal } from "@/components/marketing/scroll-reveal";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { listPlans, type PlanSummary } from "@/lib/api/client";
import { NAV_LINKS } from "@/lib/marketing/nav-links";
import { cn } from "@/lib/utils";
import { SITE_URL } from "@/app/layout";

export const metadata: Metadata = {
  title: "AgendioBR — Agendamento e gestão para o seu negócio",
  description:
    "Agenda, clientes, equipe e marca própria em um só lugar. O AgendioBR se adapta ao seu segmento — barbearia, clínica, pet shop e muito mais — sem precisar configurar nada.",
};

// Revalida a cada hora — os planos raramente mudam, mas a home nunca deve
// ficar permanentemente presa a um valor de build antigo.
export const revalidate = 3600;

// So usado se o backend estiver inacessivel no momento do build/revalidate
// (BL-04 nao pode se repetir: a home busca os planos reais em
// GET /api/billing/plans, isso aqui e so uma rede de seguranca pra nao
// derrubar a pagina inteira por causa da secao de precos). Espelha o seed
// real (PlanConfiguration.cs) — se o catalogo mudar de novo, atualizar aqui.
const FALLBACK_PLANS: PlanSummary[] = [
  { id: "fallback-essencial", name: "Essencial", priceAmount: 49.99, currency: "BRL", billingCycle: "Monthly", maxUnits: 1, maxProfessionals: 3, maxCustomers: 300, isFeatured: false },
  { id: "fallback-profissional", name: "Profissional", priceAmount: 69.99, currency: "BRL", billingCycle: "Monthly", maxUnits: 3, maxProfessionals: 10, maxCustomers: 1500, isFeatured: true },
  { id: "fallback-premium", name: "Premium", priceAmount: 99.99, currency: "BRL", billingCycle: "Monthly", maxUnits: 10, maxProfessionals: 30, maxCustomers: null, isFeatured: false },
];

function formatLimit(value: number | null, unlimitedLabel: string): string {
  return value === null ? unlimitedLabel : value.toLocaleString("pt-BR");
}

const COMPARISON_ROWS = [
  { label: "Unidades", get: (plan: PlanSummary) => formatLimit(plan.maxUnits, "Ilimitadas") },
  { label: "Profissionais", get: (plan: PlanSummary) => formatLimit(plan.maxProfessionals, "Ilimitados") },
  { label: "Clientes", get: (plan: PlanSummary) => formatLimit(plan.maxCustomers, "Ilimitados") },
] as const;

const SEGMENTS = [
  { icon: Scissors, label: "Barbearias" },
  { icon: Sparkles, label: "Salões de beleza" },
  { icon: Smile, label: "Clínicas odontológicas" },
  { icon: HeartPulse, label: "Clínicas médicas" },
  { icon: Brain, label: "Psicólogos" },
  { icon: Activity, label: "Fisioterapeutas" },
  { icon: Apple, label: "Nutricionistas" },
  { icon: Dumbbell, label: "Personal trainers" },
  { icon: PawPrint, label: "Pet shops" },
  { icon: Droplets, label: "Lava-rápidos" },
  { icon: Scale, label: "Advogados" },
  { icon: Camera, label: "Fotógrafos" },
] as const;

const FEATURES = [
  {
    icon: Calendar,
    title: "Agenda sem conflito",
    description:
      "Calendário por profissional ou sala, bloqueios, horários especiais e reagendamento — sem risco de dois clientes no mesmo horário.",
  },
  {
    icon: Users,
    title: "Clientes e equipe",
    description:
      "Histórico de cada cliente, campos personalizados por segmento, e convites para sua equipe com permissões por papel.",
  },
  {
    icon: Palette,
    title: "Sua marca, não a nossa",
    description:
      "Cor e logo do seu jeito no painel e na página pública de agendamento — validado para nunca ficar com texto ilegível.",
  },
  {
    icon: Bell,
    title: "Lembretes automáticos",
    description:
      "Confirmação e lembrete de horário sem precisar ligar para ninguém, reduzindo falta de cliente.",
  },
  {
    icon: Wallet,
    title: "Visão financeira",
    description: "Faturamento, comissão por profissional e relatórios para decidir com dado, não com achismo.",
  },
  {
    icon: Sparkles,
    title: "Vocabulário do seu segmento",
    description:
      "\"Cliente\" vira \"Paciente\" numa clínica e \"Tutor\" num pet shop — o sistema se adapta a você, não o contrário.",
  },
] as const;

// Recursos que TODO plano pago tem, sem diferenca entre eles — so o limite de
// unidades/profissionais/clientes muda por plano (ver PlanConfiguration.cs).
// Por isso essa lista e a mesma nos 3 cards, ao contrario dos limites acima.
const PLAN_FEATURES = [
  "Agenda sem conflito de horários",
  "Página pública para agendamento online",
  "Notificações automáticas por e-mail e WhatsApp",
  "Gestão completa de clientes e equipe",
  "Marca própria — logo e cores personalizadas",
  "Financeiro e relatórios completos",
  "Programa de fidelidade",
] as const;

const FAQ = [
  {
    question: "Preciso saber programar ou contratar alguém de TI para configurar?",
    answer:
      "Não. No cadastro você escolhe o segmento do seu negócio e o sistema já vem com o vocabulário e as configurações certas — sem precisar mexer em nada técnico.",
  },
  {
    question: "O AgendioBR funciona para o meu tipo de negócio?",
    answer:
      "Se o seu negócio funciona com horário marcado — barbearia, clínica, pet shop, consultoria, estúdio e muitos outros — o AgendioBR se adapta a ele.",
  },
  {
    question: "Posso cancelar quando quiser?",
    answer: "Sim, a assinatura é mensal e sem fidelidade. Você cancela quando quiser, sem multa.",
  },
  {
    question: "Meus dados e os dos meus clientes ficam seguros?",
    answer:
      "Sim. Senhas nunca ficam em texto simples, dado sensível é criptografado, e cada estabelecimento só enxerga os próprios dados — nunca de outro negócio na plataforma.",
  },
] as const;

async function getPlans(): Promise<PlanSummary[]> {
  try {
    const plans = await listPlans();
    return plans.length > 0 ? plans : FALLBACK_PLANS;
  } catch {
    return FALLBACK_PLANS;
  }
}

// WebSite + Organization no @graph -- ajuda o Google a associar o nome
// "AgendioBR" ao dominio e e pre-requisito comum pra sitelinks/sitelinks
// search box (nao garante, quem decide e o algoritmo, mas sem isso o Google
// tem menos sinal pra montar). Injetado so na home, pratica padrao do
// schema.org pra estes dois tipos.
const structuredData = {
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "WebSite",
      name: "AgendioBR",
      url: SITE_URL,
    },
    {
      "@type": "Organization",
      name: "AgendioBR",
      url: SITE_URL,
      logo: `${SITE_URL}/logo.png`,
    },
  ],
};

export default async function MarketingHomePage() {
  const plans = await getPlans();

  return (
    <div className="flex min-h-full flex-1 flex-col">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData) }} />
      <SiteHeader />

      <main className="flex-1">
        {/* Hero */}
        <section className="relative overflow-hidden">
          <div aria-hidden className="pointer-events-none absolute inset-0">
            <div className="hero-glow hero-glow-1" />
            <div className="hero-glow hero-glow-2" />
            <div className="hero-glow hero-glow-3" />
          </div>
          <CursorSpotlight />
          <div className="relative mx-auto flex max-w-6xl flex-col items-center gap-14 px-4 py-16 text-center sm:px-6 sm:py-24">
            <div className="max-w-3xl">
              <Badge variant="secondary" className="bg-accent text-accent-foreground mb-6">
                Feito para negócios com horário marcado
              </Badge>
              <h1 className="text-4xl font-semibold tracking-tight text-balance sm:text-5xl lg:text-6xl">
                Seu negócio organizado. Seus horários sob controle.
              </h1>
              <p className="text-muted-foreground mx-auto mt-6 max-w-xl text-lg text-balance">
                Gerencie agendamentos, profissionais, clientes e unidades em um único sistema, simples e
                inteligente.
              </p>
              <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
                <Button size="lg" asChild className="shadow-md">
                  <Link href="/onboarding">Cadastre-se</Link>
                </Button>
                <Button size="lg" variant="outline" asChild>
                  <a href="#showcase">Conhecer o AgendioBR</a>
                </Button>
              </div>
              <p className="text-muted-foreground mt-5 text-sm">
                Sem burocracia · Configuração rápida · Cancele quando quiser
              </p>
            </div>

            <ScrollReveal variant="scale" className="w-full">
              <HeroDashboardMockup />
            </ScrollReveal>
          </div>
        </section>

        {/* Segmentos */}
        <section id="segmentos" className="border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <ScrollReveal className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Para o seu segmento</h2>
              <p className="text-muted-foreground mt-3">
                Cada segmento já sai com o vocabulário certo — nada de &ldquo;cliente&rdquo; quando o certo é
                &ldquo;paciente&rdquo; ou &ldquo;tutor&rdquo;.
              </p>
            </ScrollReveal>
            <div className="mt-10 grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
              {SEGMENTS.map(({ icon: Icon, label }, index) => (
                <ScrollReveal key={label} delayMs={(index % 6) * 60}>
                  <Card className="group/segment transition-all duration-300 hover:-translate-y-1 hover:shadow-md">
                    <CardContent className="flex flex-col items-center gap-2 text-center">
                      <Icon
                        className="text-primary size-6 transition-transform duration-300 group-hover/segment:scale-110"
                        strokeWidth={1.75}
                      />
                      <span className="text-sm font-medium">{label}</span>
                    </CardContent>
                  </Card>
                </ScrollReveal>
              ))}
            </div>
            <p className="text-muted-foreground mt-6 text-center text-sm">
              E dezenas de outros segmentos que funcionam com horário marcado.
            </p>
          </div>
        </section>

        {/* Product showcase */}
        <section id="showcase" className="bg-muted/40 border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <ScrollReveal className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Veja o AgendioBR em ação</h2>
              <p className="text-muted-foreground mt-3">Tenha uma visão completa do seu negócio em poucos cliques.</p>
            </ScrollReveal>
            <ScrollReveal variant="scale" delayMs={120} className="mt-10">
              <ProductShowcase />
            </ScrollReveal>
          </div>
        </section>

        {/* Funcionalidades */}
        <section id="funcionalidades" className="border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <ScrollReveal className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Tudo que o seu negócio precisa</h2>
              <p className="text-muted-foreground mt-3">Sem módulo extra para comprar, sem configuração complicada.</p>
            </ScrollReveal>
            <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {FEATURES.map(({ icon: Icon, title, description }, index) => (
                <ScrollReveal key={title} delayMs={(index % 3) * 100}>
                  <Card className="group/feature h-full transition-all duration-300 hover:-translate-y-1 hover:shadow-lg hover:ring-primary/20">
                    <CardHeader>
                      <div className="bg-accent text-accent-foreground mb-2 flex size-10 items-center justify-center rounded-lg transition-transform duration-300 group-hover/feature:scale-110">
                        <Icon className="size-5" strokeWidth={1.75} />
                      </div>
                      <CardTitle className="text-base">{title}</CardTitle>
                      <CardDescription>{description}</CardDescription>
                    </CardHeader>
                  </Card>
                </ScrollReveal>
              ))}
            </div>
          </div>
        </section>

        {/* Como funciona */}
        <section id="como-funciona" className="bg-muted/40 border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <ScrollReveal className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">
                Comece a organizar seu negócio em poucos passos
              </h2>
            </ScrollReveal>
            <div className="mt-12">
              <HowItWorks />
            </div>
          </div>
        </section>

        {/* Confiança */}
        <section className="border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <TrustStats />
          </div>
        </section>

        {/* Preços */}
        <section id="precos" className="bg-muted/40 border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <ScrollReveal className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Preços simples, sem surpresa</h2>
              <p className="text-muted-foreground mt-3">Sem fidelidade. Cancele quando quiser.</p>
            </ScrollReveal>
            <div className="mx-auto mt-10 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
              {plans.map((plan, index) => {
                const isFree = plan.priceAmount === 0;
                return (
                  <ScrollReveal key={plan.id} delayMs={index * 120}>
                    <Card className={cn(plan.isFeatured && "border-primary ring-primary/20 scale-[1.02] shadow-lg ring-2")}>
                      <CardHeader>
                        {plan.isFeatured && <Badge className="mb-2">Mais popular</Badge>}
                        <CardTitle className="text-lg">{plan.name}</CardTitle>
                        <CardDescription>
                          {isFree
                            ? "Para testar o sistema sem compromisso."
                            : "14 dias de teste grátis, depois cobrança automática mensal."}
                        </CardDescription>
                        <p className="pt-2">
                          <span className="text-3xl font-semibold tracking-tight">
                            {isFree ? "Grátis" : `R$ ${plan.priceAmount.toFixed(2).replace(".", ",")}`}
                          </span>
                          {!isFree && <span className="text-muted-foreground text-sm"> /mês</span>}
                        </p>
                      </CardHeader>
                      <CardContent className="flex flex-col gap-4">
                        <ul className="grid gap-2 text-sm">
                          <li className="flex items-start gap-2">
                            <Check className="text-primary mt-0.5 size-4 shrink-0" strokeWidth={2.5} />
                            {plan.maxUnits === null
                              ? "Unidades ilimitadas"
                              : `${plan.maxUnits} unidade${plan.maxUnits === 1 ? "" : "s"}`}
                          </li>
                          <li className="flex items-start gap-2">
                            <Check className="text-primary mt-0.5 size-4 shrink-0" strokeWidth={2.5} />
                            {plan.maxProfessionals === null
                              ? "Profissionais ilimitados"
                              : `${plan.maxProfessionals} profissionais`}
                          </li>
                          <li className="flex items-start gap-2">
                            <Check className="text-primary mt-0.5 size-4 shrink-0" strokeWidth={2.5} />
                            {plan.maxCustomers === null
                              ? "Clientes ilimitados"
                              : `${plan.maxCustomers.toLocaleString("pt-BR")} clientes`}
                          </li>
                          <li className="flex items-start gap-2">
                            <Check className="text-primary mt-0.5 size-4 shrink-0" strokeWidth={2.5} />
                            Cancele quando quiser
                          </li>
                        </ul>
                        <ul className="grid gap-2 border-t pt-4 text-sm">
                          {PLAN_FEATURES.map((feature) => (
                            <li key={feature} className="flex items-start gap-2">
                              <Check className="text-primary mt-0.5 size-4 shrink-0" strokeWidth={2.5} />
                              {feature}
                            </li>
                          ))}
                        </ul>
                        <Button variant={plan.isFeatured ? "default" : "outline"} asChild className="mt-2">
                          <Link href="/onboarding">Começar com {plan.name}</Link>
                        </Button>
                      </CardContent>
                    </Card>
                  </ScrollReveal>
                );
              })}
            </div>
            <p className="text-muted-foreground mt-6 text-center text-xs">
              Valores de lançamento, sujeitos a alteração.
            </p>

            <ScrollReveal delayMs={200} className="mx-auto mt-14 max-w-4xl">
              <h3 className="text-center text-lg font-semibold">Compare os planos</h3>
              <div className="mt-6 hidden overflow-x-auto rounded-lg border sm:block">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Recurso</TableHead>
                      {plans.map((plan) => (
                        <TableHead key={plan.id} className="text-center">
                          {plan.name}
                        </TableHead>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {COMPARISON_ROWS.map((row) => (
                      <TableRow key={row.label}>
                        <TableCell className="font-medium">{row.label}</TableCell>
                        {plans.map((plan) => (
                          <TableCell key={plan.id} className="text-center">
                            {row.get(plan)}
                          </TableCell>
                        ))}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              {/* Mobile: a tabela nao cabe com 4 colunas — os limites ja aparecem nos cards acima. */}
              <p className="text-muted-foreground mt-6 text-center text-sm sm:hidden">
                Veja os limites de cada plano nos cards acima.
              </p>
            </ScrollReveal>
          </div>
        </section>

        {/* FAQ */}
        <section id="faq" className="border-t py-16 sm:py-20">
          <div className="mx-auto max-w-3xl px-4 sm:px-6">
            <ScrollReveal>
              <h2 className="text-center text-2xl font-semibold tracking-tight sm:text-3xl">
                Perguntas frequentes
              </h2>
            </ScrollReveal>
            <Accordion type="multiple" className="mt-8 grid gap-3">
              {FAQ.map(({ question, answer }) => (
                <AccordionItem key={question} value={question} className="border-border rounded-lg border bg-background px-4">
                  <AccordionTrigger>{question}</AccordionTrigger>
                  <AccordionContent className="text-muted-foreground">{answer}</AccordionContent>
                </AccordionItem>
              ))}
            </Accordion>
          </div>
        </section>

        {/* CTA final -- unica outra secao com fundo nao-solido alem do Hero
            (todas as outras usam bg-background/bg-muted lisos), entao e a
            unica que tambem ganha a atmosfera de luz + spot do cursor. */}
        <section className="relative overflow-hidden border-t py-16 sm:py-20">
          <div aria-hidden className="pointer-events-none absolute inset-0">
            <div className="hero-glow hero-glow-1" />
            <div className="hero-glow hero-glow-2" />
            <div className="hero-glow hero-glow-3" />
          </div>
          <CursorSpotlight />
          <ScrollReveal variant="scale" className="relative mx-auto max-w-2xl px-4 text-center sm:px-6">
            <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">
              Pronto para deixar seu negócio mais organizado?
            </h2>
            <p className="text-muted-foreground mt-3">
              Tenha sua agenda, clientes, profissionais e unidades organizados em um único lugar.
            </p>
            <Button size="lg" asChild className="mt-6 shadow-md">
              <Link href="/onboarding">Cadastre-se</Link>
            </Button>
          </ScrollReveal>
        </section>
      </main>

      <footer className="border-t">
        <div className="mx-auto grid max-w-6xl gap-10 px-4 py-12 sm:px-6 md:grid-cols-[1.3fr_1fr_1fr]">
          <div className="flex flex-col gap-3">
            <Logo className="text-foreground" tagline={false} />
            <p className="text-muted-foreground max-w-xs text-sm">
              O <strong className="text-foreground">sistema completo</strong> para a gestão do seu negócio: agenda,
              clientes, financeiro e estoque em um só lugar.
            </p>
          </div>

          <div className="flex flex-col gap-3">
            <h3 className="text-foreground border-primary border-l-2 pl-2 text-sm font-semibold">Produto</h3>
            <nav className="flex flex-col gap-2">
              {NAV_LINKS.map((link) => (
                <a key={link.href} href={link.href} className="text-muted-foreground hover:text-foreground text-sm transition-colors">
                  {link.label}
                </a>
              ))}
            </nav>
          </div>

          <div className="flex flex-col gap-3">
            <h3 className="text-foreground border-primary border-l-2 pl-2 text-sm font-semibold">Contato</h3>
            <a
              href="mailto:agendio.ai@gmail.com"
              className="text-muted-foreground hover:text-foreground flex items-center gap-2 text-sm transition-colors"
            >
              <Mail className="size-4" aria-hidden />
              agendio.ai@gmail.com
            </a>
          </div>
        </div>

        <div className="border-t">
          <div className="text-muted-foreground mx-auto flex max-w-6xl flex-col items-center gap-3 px-4 py-6 text-xs sm:flex-row sm:justify-between sm:px-6">
            <p>&copy; {new Date().getFullYear()} AGENDIOBR. Todos os direitos reservados.</p>
            <div className="flex flex-wrap items-center justify-center gap-x-4 gap-y-2">
              <span className="flex items-center gap-1.5">
                <ShieldCheck className="text-primary size-3.5" aria-hidden />
                Dados protegidos (LGPD)
              </span>
              <span className="flex items-center gap-1.5">
                <MessageCircle className="text-primary size-3.5" aria-hidden />
                API oficial do WhatsApp
              </span>
              <span className="flex items-center gap-1.5">
                <CreditCard className="text-primary size-3.5" aria-hidden />
                Pagamento via Asaas
              </span>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
}
