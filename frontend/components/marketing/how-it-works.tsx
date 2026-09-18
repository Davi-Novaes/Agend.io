import { ScrollReveal } from "@/components/marketing/scroll-reveal";

const STEPS = [
  { number: "01", title: "Cadastre seu estabelecimento", description: "Escolha seu segmento e o sistema já vem configurado do jeito certo." },
  { number: "02", title: "Adicione profissionais e serviços", description: "Cadastre sua equipe, os serviços oferecidos e os preços." },
  { number: "03", title: "Configure seus horários", description: "Defina expediente, folgas e regras de agendamento em minutos." },
  { number: "04", title: "Comece a receber agendamentos", description: "Compartilhe sua página pública ou agende manualmente pelo painel." },
] as const;

export function HowItWorks() {
  return (
    <div className="relative grid grid-cols-1 gap-8 sm:grid-cols-2 lg:grid-cols-4">
      <div aria-hidden className="bg-border absolute top-6 right-0 left-0 hidden h-px lg:block" />
      {STEPS.map((step, index) => (
        <ScrollReveal key={step.number} delayMs={index * 120} className="relative flex flex-col items-start gap-3">
          <span className="bg-primary text-primary-foreground ring-background relative z-10 flex size-12 shrink-0 items-center justify-center rounded-full text-sm font-semibold ring-4">
            {step.number}
          </span>
          <h3 className="text-base font-semibold">{step.title}</h3>
          <p className="text-muted-foreground text-sm">{step.description}</p>
        </ScrollReveal>
      ))}
    </div>
  );
}
