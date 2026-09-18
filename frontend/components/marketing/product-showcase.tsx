"use client";

import * as React from "react";
import { Building2, Calendar, ChevronDown, TrendingDown, TrendingUp, UserRound, Users } from "lucide-react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Sector,
  Tooltip as RechartsTooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { PieSectorDataItem } from "recharts/types/polar/Pie";

import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";

// Toda a demonstracao abaixo usa dados ficticios locais (nao vem de API) --
// decisao deliberada pra nao acoplar a home ao contrato de /lib/api/client.ts,
// que ja mudou varias vezes ao longo do projeto (ver plano do redesign). O
// painel Agenda e o donut de Relatorios seguem a MESMA receita visual dos
// componentes reais (components/agenda/appointment-chip.tsx e
// components/dashboard/appointment-status-chart.tsx) -- so os dados sao locais.

type AgendaStatus = "Confirmed" | "Scheduled" | "InProgress" | "Completed";

const AGENDA_STATUS_TONE: Record<AgendaStatus, { surface: string; badge: string; label: string }> = {
  Confirmed: { surface: "bg-success/10 border-success/30 border-l-success", badge: "bg-success/20 text-success", label: "Confirmado" },
  Scheduled: { surface: "bg-warning/10 border-warning/30 border-l-warning", badge: "bg-warning/20 text-warning", label: "Pendente" },
  InProgress: { surface: "bg-info/10 border-info/30 border-l-info", badge: "bg-info/20 text-info", label: "Em atend." },
  Completed: { surface: "bg-primary/10 border-primary/30 border-l-primary", badge: "bg-primary/20 text-primary", label: "Concluído" },
};

const AGENDA_TIMES = ["09:00", "09:30", "10:00", "10:30", "11:00", "11:30"];
const AGENDA_ROW_HEIGHT_PX = 52;

type AgendaAppointment = { start: string; durationSlots: number; customer: string; service: string; status: AgendaStatus };

const AGENDA_PROFESSIONALS: { name: string; appointments: AgendaAppointment[] }[] = [
  {
    name: "Bruno Lima",
    appointments: [
      { start: "09:00", durationSlots: 1, customer: "Marina Costa", service: "Corte", status: "Confirmed" },
      { start: "10:00", durationSlots: 2, customer: "Pedro Alves", service: "Corte + Barba", status: "InProgress" },
    ],
  },
  {
    name: "Carla Nunes",
    appointments: [
      { start: "09:30", durationSlots: 1, customer: "Julia Prado", service: "Coloração", status: "Scheduled" },
      { start: "11:00", durationSlots: 1, customer: "Fernanda Lima", service: "Escova", status: "Confirmed" },
    ],
  },
  {
    name: "Rafael Souza",
    appointments: [
      { start: "09:00", durationSlots: 1, customer: "Carlos Andrade", service: "Barba", status: "Completed" },
      { start: "10:30", durationSlots: 1, customer: "Beatriz Mendes", service: "Sobrancelha", status: "Confirmed" },
    ],
  },
];

function initials(name: string): string {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}

const STATUS_DONUT = [
  { key: "Confirmed", label: "Confirmados", count: 21, color: "var(--chart-1)" },
  { key: "Scheduled", label: "Pendentes", count: 9, color: "var(--chart-4)" },
  { key: "Completed", label: "Concluídos", count: 15, color: "var(--chart-3)" },
  { key: "Cancelled", label: "Cancelados", count: 3, color: "var(--destructive)" },
] as const;
const STATUS_DONUT_TOTAL = STATUS_DONUT.reduce((sum, item) => sum + item.count, 0);

function RevenueAreaTooltip({
  active,
  payload,
}: {
  active?: boolean;
  payload?: readonly { payload: (typeof REVENUE_SERIES_WITH_DELTA)[number] }[];
}) {
  if (!active || !payload?.[0]) return null;
  const point = payload[0].payload;
  const isPositive = point.deltaPercent !== null && point.deltaPercent >= 0;

  return (
    <div className="bg-popover text-popover-foreground min-w-40 rounded-lg border px-3.5 py-3 text-xs shadow-lg">
      <p className="font-semibold">{point.month}</p>
      <p className="text-muted-foreground mt-1 text-sm font-medium">{formatCurrency(point.revenue)}</p>
      {point.deltaPercent !== null && (
        <p className={cn("mt-1.5 flex items-center gap-1 font-medium", isPositive ? "text-success" : "text-destructive")}>
          {isPositive ? <TrendingUp className="size-3" aria-hidden /> : <TrendingDown className="size-3" aria-hidden />}
          {Math.abs(point.deltaPercent).toFixed(1)}% vs. mês anterior
        </p>
      )}
    </div>
  );
}

function StatusDonutTooltip({
  active,
  payload,
}: {
  active?: boolean;
  payload?: readonly { payload: (typeof STATUS_DONUT)[number] }[];
}) {
  if (!active || !payload?.[0]) return null;
  const segment = payload[0].payload;
  const percent = Math.round((segment.count / STATUS_DONUT_TOTAL) * 100);

  return (
    <div className="bg-popover text-popover-foreground rounded-lg border px-3 py-2 text-xs shadow-lg">
      <p className="font-semibold">{segment.label}</p>
      <p className="text-muted-foreground mt-0.5">
        {segment.count} agendamentos · {percent}%
      </p>
    </div>
  );
}

// Destructuring so por essas props (em vez de {...props}) de proposito --
// Recharts embute um "key" no objeto de props do activeShape, e espalhar tudo
// direto no elemento faz o React reclamar (key precisa ser passada direto,
// nunca via spread).
function renderActiveDonutShape({ cx, cy, innerRadius, outerRadius, startAngle, endAngle, fill }: PieSectorDataItem) {
  return (
    <Sector
      cx={cx}
      cy={cy}
      innerRadius={innerRadius}
      outerRadius={(outerRadius ?? 0) + 6}
      startAngle={startAngle}
      endAngle={endAngle}
      fill={fill}
    />
  );
}

const CUSTOMERS = [
  { name: "Marina Costa", lastVisit: "Há 3 dias", count: 12 },
  { name: "Pedro Alves", lastVisit: "Há 1 semana", count: 8 },
  { name: "Julia Prado", lastVisit: "Ontem", count: 21 },
  { name: "Carlos Andrade", lastVisit: "Há 2 semanas", count: 5 },
  { name: "Beatriz Mendes", lastVisit: "Há 2 dias", count: 14 },
  { name: "Diego Martins", lastVisit: "Há 4 dias", count: 3 },
];

const PROFESSIONALS = [
  { name: "Bruno Lima", role: "Barbeiro", occupancy: 92, weeklyAppointments: 24 },
  { name: "Carla Nunes", role: "Colorista", occupancy: 78, weeklyAppointments: 19 },
  { name: "Rafael Souza", role: "Barbeiro", occupancy: 65, weeklyAppointments: 16 },
  { name: "Fernanda Lima", role: "Esteticista", occupancy: 71, weeklyAppointments: 18 },
  { name: "Camila Rocha", role: "Manicure", occupancy: 84, weeklyAppointments: 22 },
];
const PROFESSIONALS_AVG_OCCUPANCY = Math.round(
  PROFESSIONALS.reduce((sum, professional) => sum + professional.occupancy, 0) / PROFESSIONALS.length
);

const UNITS = [
  { name: "Unidade Centro", professionals: 5, appointments: 312 },
  { name: "Unidade Norte", professionals: 3, appointments: 187 },
  { name: "Unidade Sul", professionals: 4, appointments: 245 },
];

const REVENUE_SERIES = [
  { month: "Mar", revenue: 18400 },
  { month: "Abr", revenue: 20100 },
  { month: "Mai", revenue: 21400 },
  { month: "Jun", revenue: 23800 },
  { month: "Jul", revenue: 25220 },
  { month: "Ago", revenue: 28450 },
];

// deltaPercent null no 1o mes (sem mes anterior pra comparar) -- consumido
// pelo tooltip do AreaChart e pelo indicador de tendencia no header do card.
const REVENUE_SERIES_WITH_DELTA = REVENUE_SERIES.map((point, index) => {
  const previous = REVENUE_SERIES[index - 1];
  const deltaPercent = previous ? ((point.revenue - previous.revenue) / previous.revenue) * 100 : null;
  return { ...point, deltaPercent };
});
const LATEST_REVENUE_POINT = REVENUE_SERIES_WITH_DELTA[REVENUE_SERIES_WITH_DELTA.length - 1];

const REVENUE_RANGE_OPTIONS = ["7 meses", "12 meses", "Este ano", "Ano anterior"] as const;

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatCompactCurrency(value: number): string {
  return `${Math.round(value / 1000)}k`;
}

const TABS = [
  { value: "agenda", label: "Agenda", icon: Calendar },
  { value: "clientes", label: "Clientes", icon: Users },
  { value: "profissionais", label: "Profissionais", icon: UserRound },
  { value: "unidades", label: "Unidades", icon: Building2 },
  { value: "relatorios", label: "Relatórios", icon: TrendingUp },
] as const;

const PANEL_ANIMATION =
  "data-[state=active]:animate-in data-[state=active]:fade-in-0 data-[state=active]:slide-in-from-bottom-2 data-[state=active]:duration-500";

type TabValue = (typeof TABS)[number]["value"];

// Troca sozinha de aba pra secao nao ficar parada esperando clique -- mas
// para de vez assim que a pessoa mexe (clica numa aba) e pausa temporariamente
// no hover, pra nao trocar debaixo de quem esta lendo. WCAG 2.2 AA (2.2.2/
// 2.3.3): desliga completamente com prefers-reduced-motion, mesma regra do
// resto da home (ver ScrollReveal).
const AUTO_ADVANCE_MS = 3000;

export function ProductShowcase() {
  const [activeTab, setActiveTab] = React.useState<TabValue>(TABS[0].value);
  const [isHovering, setIsHovering] = React.useState(false);
  const [hasInteracted, setHasInteracted] = React.useState(false);
  // So cosmetico: os dados sao ficticios e nao existe backend por tras dessa
  // demonstracao (ver comentario no topo do arquivo), entao trocar a opcao so
  // atualiza o rotulo -- nao finge recalcular a serie pra um periodo que nao
  // existe.
  const [revenueRange, setRevenueRange] = React.useState<(typeof REVENUE_RANGE_OPTIONS)[number]>(
    REVENUE_RANGE_OPTIONS[0]
  );
  const [prefersReducedMotion] = React.useState(
    () => typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );

  const isAutoAdvancing = !prefersReducedMotion && !hasInteracted && !isHovering;

  React.useEffect(() => {
    if (!isAutoAdvancing) return;
    const timer = setTimeout(() => {
      setActiveTab((current) => {
        const nextIndex = (TABS.findIndex((tab) => tab.value === current) + 1) % TABS.length;
        return TABS[nextIndex].value;
      });
    }, AUTO_ADVANCE_MS);
    return () => clearTimeout(timer);
  }, [activeTab, isAutoAdvancing]);

  function handleTabChange(value: string) {
    setHasInteracted(true);
    setActiveTab(value as TabValue);
  }

  return (
    <Tabs
      value={activeTab}
      onValueChange={handleTabChange}
      className="gap-6"
      onMouseEnter={() => setIsHovering(true)}
      onMouseLeave={() => setIsHovering(false)}
    >
      <TabsList className="mx-auto h-auto flex-wrap gap-1 bg-transparent p-1 sm:bg-muted sm:flex-nowrap">
        {TABS.map(({ value, label, icon: Icon }) => (
          <TabsTrigger key={value} value={value} className="gap-1.5 border data-[state=active]:border-transparent sm:border-0">
            <Icon className="size-4" aria-hidden="true" />
            {label}
          </TabsTrigger>
        ))}
      </TabsList>

      <div className="border-border/60 bg-card ring-foreground/10 relative overflow-hidden rounded-2xl border shadow-xl ring-1">
        <div className="border-border/60 bg-muted/40 flex items-center gap-1.5 border-b px-4 py-2.5" aria-hidden>
          <span className="bg-muted-foreground/30 size-2 rounded-full" />
          <span className="bg-muted-foreground/30 size-2 rounded-full" />
          <span className="bg-muted-foreground/30 size-2 rounded-full" />
        </div>
        {isAutoAdvancing && (
          <div className="bg-border/60 h-0.5 w-full" aria-hidden>
            <div
              key={activeTab}
              className="animate-tab-progress bg-primary/60 h-full"
              style={{ animationDuration: `${AUTO_ADVANCE_MS}ms` }}
            />
          </div>
        )}

        <div className="min-h-[26rem] p-4 sm:p-6">
          <TabsContent value="agenda" className={PANEL_ANIMATION}>
            <div className="overflow-x-auto">
              <div className="min-w-[36rem]">
                <div className="grid" style={{ gridTemplateColumns: `3.5rem repeat(${AGENDA_PROFESSIONALS.length}, 1fr)` }}>
                  <div />
                  {AGENDA_PROFESSIONALS.map((professional) => (
                    <div key={professional.name} className="flex items-center gap-2 px-2 pb-3">
                      <span className="bg-primary/15 text-primary flex size-7 shrink-0 items-center justify-center rounded-full text-[11px] font-medium">
                        {initials(professional.name)}
                      </span>
                      <div className="min-w-0">
                        <p className="truncate text-xs font-semibold">{professional.name}</p>
                        <p className="text-muted-foreground text-[10px]">{professional.appointments.length} agendamentos</p>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="grid" style={{ gridTemplateColumns: `3.5rem repeat(${AGENDA_PROFESSIONALS.length}, 1fr)` }}>
                  <div className="border-border/60 border-t">
                    {AGENDA_TIMES.map((time) => (
                      <div
                        key={time}
                        style={{ height: AGENDA_ROW_HEIGHT_PX }}
                        className="text-muted-foreground flex items-start justify-end pr-2 text-[10px] tabular-nums"
                      >
                        {time}
                      </div>
                    ))}
                  </div>
                  {AGENDA_PROFESSIONALS.map((professional) => (
                    <div
                      key={professional.name}
                      className="border-border/60 relative border-t border-l"
                      style={{ height: AGENDA_TIMES.length * AGENDA_ROW_HEIGHT_PX }}
                    >
                      {AGENDA_TIMES.map((time, index) => (
                        <div
                          key={time}
                          className={cn("border-border/40 absolute inset-x-0", index > 0 && "border-t")}
                          style={{ top: index * AGENDA_ROW_HEIGHT_PX, height: AGENDA_ROW_HEIGHT_PX }}
                        />
                      ))}
                      {professional.appointments.map((appointment) => {
                        const rowIndex = AGENDA_TIMES.indexOf(appointment.start);
                        if (rowIndex === -1) return null;
                        const tone = AGENDA_STATUS_TONE[appointment.status];
                        return (
                          <div
                            key={appointment.customer}
                            className={cn("absolute inset-x-1 overflow-hidden rounded-md border border-l-4 p-1.5 text-[10px] leading-tight", tone.surface)}
                            style={{ top: rowIndex * AGENDA_ROW_HEIGHT_PX + 2, height: appointment.durationSlots * AGENDA_ROW_HEIGHT_PX - 4 }}
                          >
                            <div className="flex items-center justify-between gap-1">
                              <span className="truncate font-semibold">{appointment.customer}</span>
                              <span className={cn("shrink-0 rounded px-1 py-px font-medium whitespace-nowrap", tone.badge)}>{tone.label}</span>
                            </div>
                            <p className="text-muted-foreground truncate">{appointment.service}</p>
                          </div>
                        );
                      })}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </TabsContent>

          <TabsContent value="clientes" className={PANEL_ANIMATION}>
            <ul className="flex flex-col divide-y">
              {CUSTOMERS.map((customer) => (
                <li key={customer.name} className="flex items-center gap-3 py-2.5 text-sm">
                  <span className="bg-primary/15 text-primary flex size-8 shrink-0 items-center justify-center rounded-full text-xs font-medium">
                    {customer.name
                      .split(" ")
                      .map((part) => part[0])
                      .slice(0, 2)
                      .join("")}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{customer.name}</p>
                    <p className="text-muted-foreground text-xs">Última visita: {customer.lastVisit}</p>
                  </div>
                  <Badge variant="outline" className="shrink-0">
                    {customer.count} visitas
                  </Badge>
                </li>
              ))}
            </ul>
          </TabsContent>

          <TabsContent value="profissionais" className={PANEL_ANIMATION}>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-semibold">Equipe</p>
                <p className="text-muted-foreground text-xs">{PROFESSIONALS.length} profissionais ativos</p>
              </div>
              <Badge variant="outline">{PROFESSIONALS_AVG_OCCUPANCY}% de ocupação média</Badge>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              {PROFESSIONALS.map((professional, index) => (
                <div
                  key={professional.name}
                  style={{ animationDelay: `${index * 80}ms` }}
                  className="border-border/60 animate-in fade-in-0 slide-in-from-bottom-2 fill-mode-both flex flex-col gap-3 rounded-lg border p-3 duration-500"
                >
                  <div className="flex items-center gap-2">
                    <span className="bg-primary/15 text-primary flex size-8 shrink-0 items-center justify-center rounded-full text-xs font-medium">
                      {professional.name[0]}
                    </span>
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{professional.name}</p>
                      <p className="text-muted-foreground text-xs">{professional.role}</p>
                    </div>
                  </div>
                  <div>
                    <div className="bg-muted h-1.5 overflow-hidden rounded-full">
                      <div className="bg-primary h-full rounded-full" style={{ width: `${professional.occupancy}%` }} />
                    </div>
                    <p className="text-muted-foreground mt-1 text-[10px]">{professional.occupancy}% de ocupação</p>
                  </div>
                  <p className="text-muted-foreground text-[10px]">{professional.weeklyAppointments} agendamentos essa semana</p>
                </div>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="unidades" className={PANEL_ANIMATION}>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {UNITS.map((unit) => (
                <div key={unit.name} className="border-border/60 flex flex-col gap-3 rounded-lg border p-4">
                  <div className="flex items-center gap-2">
                    <div className="bg-primary/15 text-primary shadow-[0_0_14px_-3px_color-mix(in_oklch,var(--primary),transparent_55%)] flex size-9 items-center justify-center rounded-lg">
                      <Building2 className="size-4.5" aria-hidden="true" />
                    </div>
                    <p className="font-medium">{unit.name}</p>
                  </div>
                  <div className="grid grid-cols-2 gap-2 text-sm">
                    <div>
                      <p className="text-muted-foreground text-xs">Profissionais</p>
                      <p className="font-semibold tabular-nums">{unit.professionals}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground text-xs">Agendamentos/mês</p>
                      <p className="font-semibold tabular-nums">{unit.appointments}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="relatorios" className={PANEL_ANIMATION}>
            <div className="flex flex-col gap-5">
              {/* Faturamento -- card "hero" da aba: ocupa a largura toda, com
                  hierarquia valor-grande-em-cima (padrao dashboard SaaS) em vez
                  de so um titulo pequeno, e o grafico usa quase toda a altura
                  do card em vez de ficar espremido dividindo espaco com o donut. */}
              <div className="border-border/40 bg-card shadow-primary/5 rounded-xl border p-4 shadow-lg sm:p-6">
                <div className="mb-1 flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="text-muted-foreground text-xs font-medium">Faturamento</p>
                    <p className="mt-1 text-2xl font-semibold tracking-tight tabular-nums sm:text-3xl">
                      {formatCurrency(LATEST_REVENUE_POINT.revenue)}
                    </p>
                    {LATEST_REVENUE_POINT.deltaPercent !== null && (
                      <p
                        className={cn(
                          "mt-1.5 flex items-center gap-1 text-xs font-medium",
                          LATEST_REVENUE_POINT.deltaPercent >= 0 ? "text-success" : "text-destructive"
                        )}
                      >
                        {LATEST_REVENUE_POINT.deltaPercent >= 0 ? (
                          <TrendingUp className="size-3.5" aria-hidden />
                        ) : (
                          <TrendingDown className="size-3.5" aria-hidden />
                        )}
                        {Math.abs(LATEST_REVENUE_POINT.deltaPercent).toFixed(1)}% em relação ao mês anterior
                      </p>
                    )}
                  </div>

                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <button
                        type="button"
                        className="border-border/60 text-muted-foreground hover:text-foreground hover:border-border flex shrink-0 items-center gap-1 rounded-md border px-2.5 py-1.5 text-xs font-medium transition-colors"
                      >
                        {revenueRange}
                        <ChevronDown className="size-3.5" aria-hidden />
                      </button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      {REVENUE_RANGE_OPTIONS.map((option) => (
                        <DropdownMenuItem key={option} onClick={() => setRevenueRange(option)}>
                          {option}
                        </DropdownMenuItem>
                      ))}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>

                <div className="-mx-2 mt-4 h-56 sm:h-64">
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={REVENUE_SERIES_WITH_DELTA} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                      <defs>
                        <linearGradient id="showcase-revenue-fill" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="0%" stopColor="var(--primary)" stopOpacity={0.32} />
                          <stop offset="100%" stopColor="var(--primary)" stopOpacity={0} />
                        </linearGradient>
                      </defs>
                      <CartesianGrid vertical={false} stroke="var(--border)" strokeOpacity={0.5} strokeDasharray="3 6" />
                      <XAxis
                        dataKey="month"
                        tickLine={false}
                        axisLine={false}
                        dy={10}
                        tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                      />
                      <YAxis
                        tickLine={false}
                        axisLine={false}
                        width={36}
                        tickFormatter={formatCompactCurrency}
                        tick={{ fontSize: 10, fill: "var(--muted-foreground)" }}
                      />
                      <RechartsTooltip content={<RevenueAreaTooltip />} cursor={{ stroke: "var(--primary)", strokeOpacity: 0.25 }} />
                      <Area
                        type="monotone"
                        dataKey="revenue"
                        stroke="var(--primary)"
                        strokeWidth={2.5}
                        fill="url(#showcase-revenue-fill)"
                        dot={{ r: 3, fill: "var(--card)", stroke: "var(--primary)", strokeWidth: 2 }}
                        activeDot={{ r: 5, fill: "var(--primary)", stroke: "var(--card)", strokeWidth: 2 }}
                        animationDuration={900}
                        animationEasing="ease-out"
                      />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
              </div>

              {/* Status dos agendamentos -- segundo cartao, mesma largura pra
                  manter a hierarquia (faturamento e o protagonista da aba),
                  donut+legenda centralizados dentro pra nao esticar feio numa
                  largura maior do que o conteudo pede. */}
              <div className="border-border/40 bg-card shadow-primary/5 rounded-xl border p-4 shadow-lg sm:p-6">
                <p className="text-sm font-semibold">Status dos agendamentos</p>
                <div className="mx-auto mt-4 flex max-w-md flex-col items-center gap-6 sm:flex-row">
                  <div className="relative size-32 shrink-0">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie
                          data={STATUS_DONUT as unknown as { label: string; count: number; color: string }[]}
                          dataKey="count"
                          nameKey="label"
                          cx="50%"
                          cy="50%"
                          innerRadius={42}
                          outerRadius={62}
                          paddingAngle={3}
                          stroke="var(--card)"
                          strokeWidth={2}
                          activeShape={renderActiveDonutShape}
                          animationDuration={800}
                        >
                          {STATUS_DONUT.map((segment) => (
                            <Cell key={segment.key} fill={segment.color} />
                          ))}
                        </Pie>
                        <RechartsTooltip content={<StatusDonutTooltip />} />
                      </PieChart>
                    </ResponsiveContainer>
                    <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
                      <span className="text-2xl font-semibold tabular-nums">{STATUS_DONUT_TOTAL}</span>
                      <span className="text-muted-foreground text-[10px]">Agendamentos</span>
                    </div>
                  </div>
                  <ul className="flex w-full min-w-0 flex-1 flex-col gap-2.5">
                    {STATUS_DONUT.map((segment) => {
                      const percent = Math.round((segment.count / STATUS_DONUT_TOTAL) * 100);
                      return (
                        <li key={segment.key} className="flex items-center gap-2 text-sm">
                          <span
                            aria-hidden="true"
                            className="inline-block size-2.5 shrink-0 rounded-full"
                            style={{ backgroundColor: segment.color }}
                          />
                          <span className="min-w-0 flex-1 truncate font-medium">{segment.label}</span>
                          <span className="tabular-nums font-semibold">{segment.count}</span>
                          <span className="text-muted-foreground w-9 shrink-0 text-right text-xs tabular-nums">{percent}%</span>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              </div>
            </div>
          </TabsContent>
        </div>
      </div>
    </Tabs>
  );
}
