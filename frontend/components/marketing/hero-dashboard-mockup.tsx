"use client";

import * as React from "react";
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis } from "recharts";
import { Bell, CalendarCheck, TrendingUp, Users, UsersRound } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

// Dados fictícios só para a prévia visual do produto na home -- não vem de
// nenhuma API, propositalmente desacoplado do contrato real (ver plano do
// redesign). O RECHARTS em si é reaproveitado (já é dependência do projeto),
// só os dados e os tipos são locais -- a mesma receita visual do RevenueChart
// real (components/dashboard/revenue-chart.tsx), pra parecer genuinamente
// uma tela do Agendio.
// tone: mesmo padrão do TONE_CLASSES real (components/dashboard/metric-card.tsx)
// -- bg/15+text tinge o badge, shadow-[...] acrescenta o glow pequeno na cor
// da categoria, pra essa prévia bater com o painel de verdade.
const KPIS = [
  {
    icon: CalendarCheck,
    label: "Agendamentos hoje",
    value: "48",
    delta: "+12%",
    tone: "bg-primary/15 text-primary shadow-[0_0_14px_-3px_color-mix(in_oklch,var(--primary),transparent_55%)]",
  },
  {
    icon: UsersRound,
    label: "Profissionais ativos",
    value: "8",
    delta: "+1",
    tone: "bg-success/15 text-success shadow-[0_0_14px_-3px_color-mix(in_oklch,var(--success),transparent_55%)]",
  },
  {
    icon: Users,
    label: "Clientes",
    value: "1.284",
    delta: "+8%",
    tone: "bg-info/15 text-info shadow-[0_0_14px_-3px_color-mix(in_oklch,var(--info),transparent_55%)]",
  },
  {
    icon: TrendingUp,
    label: "Ocupação",
    value: "87%",
    delta: "+5%",
    tone: "bg-warning/15 text-warning shadow-[0_0_14px_-3px_color-mix(in_oklch,var(--warning),transparent_55%)]",
  },
];

const AGENDA_ROWS = [
  { time: "09:00", name: "Marina Costa", service: "Corte + Barba" },
  { time: "09:30", name: "Rafael Souza", service: "Sobrancelha" },
  { time: "10:15", name: "Julia Prado", service: "Coloração" },
  { time: "11:00", name: "Pedro Alves", service: "Corte" },
];

const REVENUE_DATA = [
  { month: "Fev", received: 8200 },
  { month: "Mar", received: 9100 },
  { month: "Abr", received: 8700 },
  { month: "Mai", received: 10400 },
  { month: "Jun", received: 11800 },
  { month: "Jul", received: 12600 },
  { month: "Ago", received: 14200 },
];

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL", maximumFractionDigits: 0 });
}

function RevenueTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: readonly { value?: number }[];
  label?: string | number;
}) {
  const value = payload?.[0]?.value;
  if (!active || value === undefined) return null;
  return (
    <div className="bg-popover text-popover-foreground rounded-md border px-2.5 py-1.5 text-[11px] shadow-md">
      <p className="font-medium">{label}</p>
      <p className="text-muted-foreground">{formatCurrency(value)}</p>
    </div>
  );
}

/**
 * Mockup grande do painel -- e a peca central da primeira tela (a pedido do
 * usuario), entao ganha bastante espaco e detalhe, nao um widget lateral
 * compacto. Ver ProductShowcase pra tour interativo mais abaixo na pagina.
 */
export function HeroDashboardMockup() {
  const [prefersReducedMotion] = React.useState(
    () => typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
  const [firstRowConfirmed, setFirstRowConfirmed] = React.useState(prefersReducedMotion);
  const [showNotification, setShowNotification] = React.useState(false);
  const wrapperRef = React.useRef<HTMLDivElement>(null);
  const [tilt, setTilt] = React.useState({ x: 0, y: 0 });

  React.useEffect(() => {
    if (prefersReducedMotion) return;
    const confirmTimer = setTimeout(() => setFirstRowConfirmed(true), 1800);
    const notifyOnTimer = setTimeout(() => setShowNotification(true), 2200);
    const notifyOffTimer = setTimeout(() => setShowNotification(false), 6000);
    return () => {
      clearTimeout(confirmTimer);
      clearTimeout(notifyOnTimer);
      clearTimeout(notifyOffTimer);
    };
  }, [prefersReducedMotion]);

  function handleMouseMove(event: React.MouseEvent<HTMLDivElement>) {
    if (!window.matchMedia("(hover: hover)").matches) return;
    const rect = wrapperRef.current?.getBoundingClientRect();
    if (!rect) return;
    const px = (event.clientX - rect.left) / rect.width - 0.5;
    const py = (event.clientY - rect.top) / rect.height - 0.5;
    setTilt({ x: px * 4, y: py * -4 });
  }

  return (
    <div
      ref={wrapperRef}
      onMouseMove={handleMouseMove}
      onMouseLeave={() => setTilt({ x: 0, y: 0 })}
      className="relative mx-auto w-full max-w-4xl"
    >
      <div aria-hidden className="bg-primary/25 absolute -inset-10 -z-10 rounded-[3rem] blur-3xl" />

      <div
        style={{ transform: `perspective(1400px) rotateX(${tilt.y}deg) rotateY(${tilt.x}deg)` }}
        className="border-border/60 bg-card ring-foreground/10 relative overflow-hidden rounded-2xl border shadow-2xl ring-1 transition-transform duration-200 ease-out"
      >
        <div className="border-border/60 bg-muted/40 flex items-center justify-between border-b px-5 py-3">
          <div className="flex items-center gap-1.5" aria-hidden>
            <span className="bg-muted-foreground/30 size-2.5 rounded-full" />
            <span className="bg-muted-foreground/30 size-2.5 rounded-full" />
            <span className="bg-muted-foreground/30 size-2.5 rounded-full" />
          </div>
          <span className="text-muted-foreground text-sm font-medium">Painel · Hoje</span>
          <Bell className="text-muted-foreground size-4" aria-hidden="true" />
        </div>

        <div className="flex flex-col gap-4 p-5 sm:p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {KPIS.map((kpi, index) => (
              <div
                key={kpi.label}
                style={{ animationDelay: `${index * 120}ms` }}
                className="animate-in fade-in-0 slide-in-from-bottom-2 fill-mode-both border-border/60 flex flex-col gap-2 rounded-lg border p-3 duration-700"
              >
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground truncate text-[11px]">{kpi.label}</span>
                  <div className={cn("flex size-6 shrink-0 items-center justify-center rounded-md", kpi.tone)}>
                    <kpi.icon className="size-3.5" aria-hidden="true" />
                  </div>
                </div>
                <div className="flex items-baseline gap-1.5">
                  <span className="text-lg leading-none font-semibold tabular-nums">{kpi.value}</span>
                  <Badge variant="success" className="h-4 px-1 text-[9px]">
                    {kpi.delta}
                  </Badge>
                </div>
              </div>
            ))}
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-5">
            <div
              style={{ animationDelay: "500ms" }}
              className="border-border/60 animate-in fade-in-0 slide-in-from-bottom-2 fill-mode-both rounded-lg border p-4 duration-700 sm:col-span-2"
            >
              <p className="mb-1 text-sm font-semibold">Faturamento</p>
              <p className="text-muted-foreground mb-2 text-[11px]">Últimos 7 meses</p>
              <div className="h-28 w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={REVENUE_DATA} margin={{ top: 4, right: 0, left: 0, bottom: 0 }}>
                    <defs>
                      <linearGradient id="hero-revenue-fill" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="var(--chart-1)" stopOpacity={0.35} />
                        <stop offset="100%" stopColor="var(--chart-1)" stopOpacity={0.03} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid vertical={false} stroke="var(--border)" />
                    <XAxis
                      dataKey="month"
                      tick={{ fontSize: 10, fill: "var(--muted-foreground)" }}
                      axisLine={{ stroke: "var(--border)" }}
                      tickLine={false}
                    />
                    <Tooltip content={(props) => <RevenueTooltip active={props.active} payload={props.payload as never} label={props.label} />} />
                    <Area
                      type="monotone"
                      dataKey="received"
                      stroke="var(--chart-1)"
                      strokeWidth={2}
                      fill="url(#hero-revenue-fill)"
                      isAnimationActive={false}
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div
              style={{ animationDelay: "700ms" }}
              // border-l-2 border-l-primary/60: mesmo acento do TodayAgendaCard real
              // (components/dashboard/today-agenda-card.tsx) -- da mais peso visual
              // a este card em relacao aos vizinhos, igual no painel de verdade.
              className="border-border/60 animate-in fade-in-0 slide-in-from-bottom-2 fill-mode-both border-l-primary/60 flex flex-col overflow-hidden rounded-lg border border-l-2 duration-700 sm:col-span-3"
            >
              <div className="border-border/60 flex items-center justify-between border-b px-3.5 py-2.5">
                <span className="text-sm font-semibold">Agenda de hoje</span>
                <Badge variant="outline">4 agendamentos</Badge>
              </div>
              <div className="flex flex-col divide-y">
                {AGENDA_ROWS.map((row, index) => (
                  <div key={row.time} className="flex items-center gap-2.5 px-3.5 py-2.5 text-xs">
                    <span
                      aria-hidden="true"
                      className={cn(
                        "size-1.5 shrink-0 rounded-full",
                        index === 0 && !firstRowConfirmed ? "bg-muted-foreground" : "bg-success"
                      )}
                    />
                    <span className="w-10 shrink-0 font-medium tabular-nums">{row.time}</span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-medium">{row.name}</p>
                      <p className="text-muted-foreground truncate text-[10px]">{row.service}</p>
                    </div>
                    {index === 0 ? (
                      <Badge variant={firstRowConfirmed ? "success" : "outline"} className="shrink-0 transition-colors duration-500">
                        {firstRowConfirmed ? "Confirmado" : "Pendente"}
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="shrink-0">
                        Confirmado
                      </Badge>
                    )}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>

      <div
        aria-hidden={!showNotification}
        className={cn(
          "border-border bg-card absolute -right-4 -bottom-4 flex items-center gap-2 rounded-lg border px-3 py-2 text-xs shadow-lg transition-all duration-500 sm:-right-6",
          showNotification ? "translate-y-0 opacity-100" : "pointer-events-none translate-y-2 opacity-0"
        )}
      >
        <div className="bg-success/15 text-success flex size-6 shrink-0 items-center justify-center rounded-full">
          <CalendarCheck className="size-3.5" aria-hidden="true" />
        </div>
        <div>
          <p className="font-medium">Novo agendamento</p>
          <p className="text-muted-foreground text-[10px]">Julia Prado · 10:15</p>
        </div>
      </div>
    </div>
  );
}
