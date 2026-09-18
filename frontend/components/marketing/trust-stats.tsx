import { AnimatedCounter } from "@/components/marketing/animated-counter";
import { ScrollReveal } from "@/components/marketing/scroll-reveal";

// Numeros demonstrativos -- o Agendio ainda nao tem esse volume real de uso
// pra expor publicamente. Estruturado pra ser trocado por dados reais assim
// que existirem (ver secao 9 do pedido de redesign).
const STATS = [
  { value: 12480, suffix: "+", label: "Agendamentos gerenciados" },
  { value: 340, suffix: "+", label: "Estabelecimentos usando" },
  { value: 1900, suffix: "+", label: "Profissionais cadastrados" },
  { value: 98, suffix: "%", label: "Satisfação dos usuários" },
] as const;

export function TrustStats() {
  return (
    <div className="grid grid-cols-2 gap-6 sm:grid-cols-4">
      {STATS.map((stat, index) => (
        <ScrollReveal key={stat.label} delayMs={index * 100} className="text-center">
          <p className="text-primary text-3xl font-semibold tracking-tight sm:text-4xl">
            <AnimatedCounter value={stat.value} suffix={stat.suffix} />
          </p>
          <p className="text-muted-foreground mt-1 text-sm">{stat.label}</p>
        </ScrollReveal>
      ))}
    </div>
  );
}
