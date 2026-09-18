import { Building2, CalendarClock, CheckCircle2, TrendingUp, Users } from "lucide-react";

import { Logo } from "@/components/logo";
import { cn } from "@/lib/utils";

// Fundo na cor de marca (--primary) -- a mesma usada em botoes/links no resto
// do produto, nao uma cor inventada so pra esta tela. A pagina de login forca
// o tema claro (ver login/page.tsx), entao --primary aqui e sempre o roxo
// claro do :root, nunca o tom do dark mode -- e assim que este painel fica
// visualmente estavel mesmo se o dono tiver trocado pra dark mode no painel
// dele antes de sair.
//
// Cards flutuantes: composicao visual de fundo (agenda, clientes, faturamento,
// confirmacao), NAO um dashboard funcional -- por isso pointer-events-none e
// so aparecem em telas bem largas (xl+), onde ha espaco de sobra sem competir
// com o titulo/subtitulo. Cada um flutua devagar e fora de sincronia com os
// outros (durar/delay distintos) via as keyframes login-float-up/down do
// globals.css.
const FLOATING_CARDS = [
  {
    icon: CalendarClock,
    label: "Próximo agendamento",
    value: "Hoje, 14:30",
    detail: "Corte + Barba",
    className: "top-[16%] right-[7%] motion-safe:animate-[login-float-up_9s_ease-in-out_infinite]",
  },
  {
    icon: Users,
    label: "Clientes ativos",
    value: "1.284",
    detail: "+8% este mês",
    className: "top-[46%] right-[1%] motion-safe:animate-[login-float-down_11s_ease-in-out_infinite] [animation-delay:1s]",
  },
  {
    icon: TrendingUp,
    label: "Faturamento",
    value: "R$ 3.240",
    detail: "este mês",
    className: "bottom-[16%] left-[4%] motion-safe:animate-[login-float-up_10s_ease-in-out_infinite] [animation-delay:2s]",
  },
] as const;

export function LoginShowcase() {
  return (
    <section className="bg-primary text-primary-foreground relative hidden flex-col justify-between overflow-hidden p-10 shadow-[16px_0_48px_-24px_rgba(0,0,0,0.35)] lg:flex lg:p-12 xl:p-16">
      {/* Variacao tonal dentro do proprio matiz da marca (nao um flat fill) --
          da profundidade sem introduzir uma cor nova. */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 bg-gradient-to-br from-[color-mix(in_oklch,var(--primary),black_18%)] via-primary to-[color-mix(in_oklch,var(--primary),white_10%)]"
      />

      {/* Dois campos de luz ambiente, grandes e bem difusos -- cobrem boa
          parte do painel (nao um unico ponto concentrado) e derivam devagar. */}
      <div
        aria-hidden
        className="pointer-events-none absolute -top-32 -left-24 size-[28rem] rounded-full bg-[color-mix(in_oklch,var(--primary-foreground),transparent_88%)] blur-3xl motion-safe:animate-[login-glow-drift_24s_ease-in-out_infinite]"
      />
      <div
        aria-hidden
        className="pointer-events-none absolute -right-20 -bottom-36 size-[32rem] rounded-full bg-[color-mix(in_oklch,var(--primary-foreground),transparent_90%)] blur-3xl motion-safe:animate-[login-glow-drift_28s_ease-in-out_infinite] [animation-delay:4s]"
      />

      {/* Formas abstratas (arcos/circulo), bem sutis -- mesma linguagem do
          print de referencia, sem competir com o texto. */}
      <svg aria-hidden className="pointer-events-none absolute inset-0 size-full opacity-[0.14]" viewBox="0 0 600 800" fill="none">
        <circle cx="470" cy="150" r="130" stroke="currentColor" strokeWidth="1.5" />
        <path d="M 410 30 A 210 210 0 0 1 550 250" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        <path d="M 370 -10 A 250 250 0 0 1 570 320" stroke="currentColor" strokeWidth="1" strokeLinecap="round" />
      </svg>

      {/* Cards flutuantes -- vidro translucido, so a partir de xl (espaco de
          sobra pra nao brigar com o titulo em telas menores). */}
      <div aria-hidden className="pointer-events-none absolute inset-0 hidden xl:block">
        {FLOATING_CARDS.map((card) => (
          <div
            key={card.label}
            className={cn(
              "border-primary-foreground/15 bg-primary-foreground/10 absolute flex w-44 items-center gap-2.5 rounded-xl border p-3 shadow-lg backdrop-blur-md",
              card.className
            )}
          >
            <span className="bg-primary-foreground/15 flex size-8 shrink-0 items-center justify-center rounded-lg">
              <card.icon className="size-4" aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="text-primary-foreground/60 truncate text-[10px] font-medium tracking-wide uppercase">
                {card.label}
              </p>
              <p className="truncate text-sm font-semibold">{card.value}</p>
              <p className="text-primary-foreground/55 truncate text-[10px]">{card.detail}</p>
            </div>
          </div>
        ))}

        {/* bottom-right, espelhando o card de faturamento (bottom-left) --
            antes ficava em top-[36%] left-[8%], bem em cima do titulo
            (colisao real, reportada pelo usuario num viewport largo). */}
        <div className="border-primary-foreground/15 bg-primary-foreground/10 absolute right-[9%] bottom-[15%] flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-medium shadow-lg backdrop-blur-md motion-safe:animate-[login-float-down_8s_ease-in-out_infinite]">
          <CheckCircle2 className="size-3.5" aria-hidden />
          Atendimento confirmado
        </div>
      </div>

      <div className="relative z-10">
        <Logo inverted className="text-primary-foreground text-lg" />
      </div>

      <div className="relative z-10 flex max-w-md flex-1 flex-col justify-center gap-4 py-10">
        <span className="border-primary-foreground/20 bg-primary-foreground/10 inline-flex w-fit items-center rounded-full border px-3 py-1 text-xs font-medium backdrop-blur-sm">
          Feito para negócios com horário marcado
        </span>
        <h1 className="text-4xl leading-[1.1] font-semibold text-balance xl:text-[2.75rem]">
          Seu negócio organizado.
          <br />
          Sua rotina simplificada.
        </h1>
        <p className="text-primary-foreground/75 max-w-sm text-base text-balance">
          Agenda, clientes, equipe e faturamento em uma única plataforma — sem complicar a rotina do seu negócio.
        </p>
      </div>

      <div className="border-primary-foreground/15 text-primary-foreground/60 relative z-10 flex items-center gap-2 border-t pt-6 text-xs">
        <Building2 className="size-3.5" aria-hidden />
        Barbearias, clínicas, pet shops, estúdios e muito mais.
      </div>
    </section>
  );
}
