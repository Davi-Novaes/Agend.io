import { Headphones, LockKeyhole, Settings2, Zap } from "lucide-react";

import { ScrollReveal } from "@/components/marketing/scroll-reveal";

const TRUST_ITEMS = [
  {
    icon: Zap,
    title: "Pronto para usar",
    description: "Escolha seu segmento e comece com configurações que já fazem sentido para o seu negócio.",
  },
  {
    icon: Settings2,
    title: "Tudo em um só lugar",
    description: "Agenda, equipe, clientes, financeiro, estoque e relatórios conversando entre si.",
  },
  {
    icon: LockKeyhole,
    title: "Dados protegidos",
    description: "Cada estabelecimento acessa somente os próprios dados, com proteção em várias camadas.",
  },
  {
    icon: Headphones,
    title: "Ajuda quando precisar",
    description: "Uma central de ajuda clara e suporte para você não ficar parado durante a operação.",
  },
] as const;

export function TrustStats() {
  return (
    <div>
      <ScrollReveal className="mx-auto max-w-2xl text-center">
        <p className="text-primary text-sm font-semibold tracking-wide uppercase">Feito para a rotina real</p>
        <h2 className="mt-3 text-2xl font-semibold tracking-tight sm:text-3xl">
          Simples para começar. Completo para acompanhar seu crescimento.
        </h2>
      </ScrollReveal>

      <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {TRUST_ITEMS.map(({ icon: Icon, title, description }, index) => (
          <ScrollReveal key={title} delayMs={index * 80}>
            <div className="marketing-card border-border/70 bg-card/60 h-full rounded-2xl border p-5 transition-all duration-300 hover:-translate-y-1 hover:shadow-lg">
              <div className="bg-primary/10 text-primary flex size-10 items-center justify-center rounded-xl">
                <Icon className="size-5" strokeWidth={1.8} aria-hidden />
              </div>
              <h3 className="mt-4 font-semibold">{title}</h3>
              <p className="text-muted-foreground mt-2 text-sm leading-relaxed">{description}</p>
            </div>
          </ScrollReveal>
        ))}
      </div>
    </div>
  );
}
