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
  Droplets,
  Dumbbell,
  HeartPulse,
  Palette,
  PawPrint,
  Scale,
  Scissors,
  Smile,
  Sparkles,
  Users,
  Wallet,
} from "lucide-react";

import { Logo } from "@/components/logo";
import { MobileNav } from "@/components/marketing/mobile-nav";
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

export const metadata: Metadata = {
  title: "Agendio — Agendamento e gestão para o seu negócio",
  description:
    "Agenda, clientes, equipe e marca própria em um só lugar. O Agendio se adapta ao seu segmento — barbearia, clínica, pet shop e muito mais — sem precisar configurar nada.",
};

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

const PLANS = [
  {
    name: "Essencial",
    price: "R$ 49,90",
    description: "Para quem está começando e trabalha sozinho.",
    features: ["1 profissional", "Agenda e clientes ilimitados", "Portal de agendamento online", "Suporte por e-mail"],
    highlighted: false,
  },
  {
    name: "Profissional",
    price: "R$ 99,90",
    description: "O mais escolhido por quem já tem equipe.",
    features: [
      "Até 5 profissionais",
      "Marca personalizada (cor e logo)",
      "Lembretes automáticos",
      "Convite de equipe com permissões",
      "Suporte prioritário",
    ],
    highlighted: true,
  },
  {
    name: "Avançado",
    price: "R$ 199,90",
    description: "Para redes e negócios com múltiplas unidades.",
    features: ["Profissionais ilimitados", "Múltiplas unidades", "Relatórios financeiros completos", "Suporte dedicado"],
    highlighted: false,
  },
] as const;

const FAQ = [
  {
    question: "Preciso saber programar ou contratar alguém de TI para configurar?",
    answer:
      "Não. No cadastro você escolhe o segmento do seu negócio e o sistema já vem com o vocabulário e as configurações certas — sem precisar mexer em nada técnico.",
  },
  {
    question: "O Agendio funciona para o meu tipo de negócio?",
    answer:
      "Se o seu negócio funciona com horário marcado — barbearia, clínica, pet shop, consultoria, estúdio e muitos outros — o Agendio se adapta a ele.",
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

export default function MarketingHomePage() {
  return (
    <div className="flex min-h-full flex-1 flex-col">
      <header className="bg-background/80 relative sticky top-0 z-20 border-b backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-6">
          <Logo />

          <nav className="hidden items-center gap-6 sm:flex">
            <a href="#segmentos" className="text-muted-foreground hover:text-foreground text-sm">
              Segmentos
            </a>
            <a href="#funcionalidades" className="text-muted-foreground hover:text-foreground text-sm">
              Funcionalidades
            </a>
            <a href="#precos" className="text-muted-foreground hover:text-foreground text-sm">
              Preços
            </a>
            <a href="#faq" className="text-muted-foreground hover:text-foreground text-sm">
              Perguntas frequentes
            </a>
          </nav>

          <div className="hidden items-center gap-2 sm:flex">
            <Button variant="ghost" asChild>
              <Link href="/login">Entrar</Link>
            </Button>
            <Button asChild>
              <Link href="/onboarding">Criar minha conta</Link>
            </Button>
          </div>

          <MobileNav />
        </div>
      </header>

      <main className="flex-1">
        {/* Hero */}
        <section className="relative overflow-hidden">
          <div
            aria-hidden
            className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_20%_10%,color-mix(in_oklch,var(--primary),transparent_92%),transparent_55%),radial-gradient(circle_at_85%_30%,color-mix(in_oklch,var(--primary),transparent_94%),transparent_60%)]"
          />
          <div className="relative mx-auto max-w-4xl px-4 py-20 text-center sm:px-6 sm:py-28">
            <Badge variant="secondary" className="mb-6 bg-accent text-accent-foreground">
              Feito para negócios com horário marcado
            </Badge>
            <h1 className="text-4xl font-semibold tracking-tight text-balance sm:text-6xl">
              O sistema de agendamento que se adapta ao seu negócio
            </h1>
            <p className="text-muted-foreground mx-auto mt-6 max-w-2xl text-lg text-balance">
              Agenda, clientes, equipe e marca própria em um só lugar — configurado do jeito certo para
              barbearia, clínica, pet shop ou o que você faz, sem precisar mexer em nada técnico.
            </p>
            <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
              <Button size="lg" asChild className="shadow-md">
                <Link href="/onboarding">Criar minha conta grátis</Link>
              </Button>
              <Button size="lg" variant="outline" asChild>
                <Link href="/login">Já tenho conta</Link>
              </Button>
            </div>
          </div>
        </section>

        {/* Segmentos */}
        <section id="segmentos" className="border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <div className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Para o seu segmento</h2>
              <p className="text-muted-foreground mt-3">
                Cada segmento já sai com o vocabulário certo — nada de &ldquo;cliente&rdquo; quando o certo é
                &ldquo;paciente&rdquo; ou &ldquo;tutor&rdquo;.
              </p>
            </div>
            <div className="mt-10 grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
              {SEGMENTS.map(({ icon: Icon, label }) => (
                <Card key={label}>
                  <CardContent className="flex flex-col items-center gap-2 text-center">
                    <Icon className="text-primary size-6" strokeWidth={1.75} />
                    <span className="text-sm font-medium">{label}</span>
                  </CardContent>
                </Card>
              ))}
            </div>
            <p className="text-muted-foreground mt-6 text-center text-sm">
              E dezenas de outros segmentos que funcionam com horário marcado.
            </p>
          </div>
        </section>

        {/* Funcionalidades */}
        <section id="funcionalidades" className="bg-muted/40 border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <div className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Tudo que o seu negócio precisa</h2>
              <p className="text-muted-foreground mt-3">Sem módulo extra para comprar, sem configuração complicada.</p>
            </div>
            <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {FEATURES.map(({ icon: Icon, title, description }) => (
                <Card key={title}>
                  <CardHeader>
                    <div className="bg-accent text-accent-foreground mb-2 flex size-10 items-center justify-center rounded-lg">
                      <Icon className="size-5" strokeWidth={1.75} />
                    </div>
                    <CardTitle className="text-base">{title}</CardTitle>
                    <CardDescription>{description}</CardDescription>
                  </CardHeader>
                </Card>
              ))}
            </div>
          </div>
        </section>

        {/* Preços */}
        <section id="precos" className="border-t py-16 sm:py-20">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <div className="mx-auto max-w-2xl text-center">
              <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">Preços simples, sem surpresa</h2>
              <p className="text-muted-foreground mt-3">Sem fidelidade. Cancele quando quiser.</p>
            </div>
            <div className="mt-10 grid gap-6 lg:grid-cols-3">
              {PLANS.map((plan) => (
                <Card key={plan.name} className={plan.highlighted ? "border-primary shadow-md" : undefined}>
                  <CardHeader>
                    {plan.highlighted && <Badge className="mb-2">Mais escolhido</Badge>}
                    <CardTitle className="text-lg">{plan.name}</CardTitle>
                    <CardDescription>{plan.description}</CardDescription>
                    <p className="pt-2">
                      <span className="text-3xl font-semibold tracking-tight">{plan.price}</span>
                      <span className="text-muted-foreground text-sm"> /mês</span>
                    </p>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <ul className="grid gap-2 text-sm">
                      {plan.features.map((feature) => (
                        <li key={feature} className="flex items-start gap-2">
                          <Check className="text-primary mt-0.5 size-4 shrink-0" strokeWidth={2.5} />
                          {feature}
                        </li>
                      ))}
                    </ul>
                    <Button variant={plan.highlighted ? "default" : "outline"} asChild className="mt-2">
                      <Link href="/onboarding">Começar com {plan.name}</Link>
                    </Button>
                  </CardContent>
                </Card>
              ))}
            </div>
            <p className="text-muted-foreground mt-6 text-center text-xs">
              Valores de lançamento, sujeitos a alteração.
            </p>
          </div>
        </section>

        {/* FAQ */}
        <section id="faq" className="bg-muted/40 border-t py-16 sm:py-20">
          <div className="mx-auto max-w-3xl px-4 sm:px-6">
            <h2 className="text-center text-2xl font-semibold tracking-tight sm:text-3xl">
              Perguntas frequentes
            </h2>
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

        {/* CTA final */}
        <section className="border-t py-16 sm:py-20">
          <div className="mx-auto max-w-2xl px-4 text-center sm:px-6">
            <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">
              Pronto para simplificar a agenda do seu negócio?
            </h2>
            <p className="text-muted-foreground mt-3">
              Leva menos de 3 minutos para configurar o seu estabelecimento.
            </p>
            <Button size="lg" asChild className="mt-6">
              <Link href="/onboarding">Criar minha conta grátis</Link>
            </Button>
          </div>
        </section>
      </main>

      <footer className="border-t py-10">
        <div className="text-muted-foreground mx-auto flex max-w-6xl flex-col items-center gap-4 px-4 text-sm sm:flex-row sm:justify-between sm:px-6">
          <Logo className="text-foreground" />
          <p>&copy; {new Date().getFullYear()} Agendio. Todos os direitos reservados.</p>
        </div>
      </footer>
    </div>
  );
}
