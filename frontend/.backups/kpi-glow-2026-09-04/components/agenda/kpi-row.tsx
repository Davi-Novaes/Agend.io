import type { LucideIcon } from "lucide-react";
import { CalendarCheck, CheckCircle2, Clock, PlayCircle, Wallet } from "lucide-react";

import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { AppointmentSummary } from "@/lib/api/client";

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

const TONES = {
  primary: "bg-primary/15 text-primary",
  success: "bg-success/15 text-success",
  warning: "bg-warning/15 text-warning",
  info: "bg-info/15 text-info",
  destructive: "bg-destructive/15 text-destructive",
} as const;

/** Compacto de proposito: sao 5 lado a lado, precisam caber sem esmagar. */
function KpiCard({
  icon: Icon,
  title,
  value,
  hint,
  tone,
  isLoading,
}: {
  icon: LucideIcon;
  title: string;
  value: string;
  hint?: string;
  tone: keyof typeof TONES;
  isLoading: boolean;
}) {
  return (
    <Card className="py-0">
      <CardContent className="flex items-center gap-3 px-3 py-3">
        <div className={`flex size-9 shrink-0 items-center justify-center rounded-lg ${TONES[tone]}`}>
          <Icon className="size-4.5" aria-hidden="true" />
        </div>
        <div className="min-w-0">
          <p className="text-muted-foreground truncate text-xs">{title}</p>
          {isLoading ? (
            <Skeleton className="mt-1 h-6 w-16" />
          ) : (
            <p className="truncate text-xl leading-tight font-semibold tabular-nums">{value}</p>
          )}
          {hint && !isLoading && <p className="text-muted-foreground truncate text-[10px] tracking-wide uppercase">{hint}</p>}
        </div>
      </CardContent>
    </Card>
  );
}

// Sempre "hoje", independente do dia/semana/mes selecionado no grid — mesma
// decisao do Painel (todayAppointmentsQuery em painel/page.tsx), pra nao
// confundir "o que acontece hoje" com "o que estou navegando agora".
export function AgendaKpiRow({
  appointmentsToday,
  isLoading,
}: {
  appointmentsToday: AppointmentSummary[] | undefined;
  isLoading: boolean;
}) {
  const items = appointmentsToday ?? [];
  const total = items.length;
  const confirmed = items.filter((a) => a.status === "Confirmed").length;
  const pending = items.filter((a) => a.status === "Scheduled").length;
  const inProgress = items.filter((a) => a.status === "InProgress").length;
  const estimatedRevenue = items
    .filter((a) => a.status !== "CancelledByCustomer" && a.status !== "CancelledByStaff" && a.status !== "NoShow")
    .reduce((sum, a) => sum + a.price, 0);

  const share = (count: number) => (total > 0 ? `${Math.round((count / total) * 100)}%` : "—");

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
      <KpiCard icon={CalendarCheck} title="Agendamentos" value={String(total)} hint="Hoje" tone="primary" isLoading={isLoading} />
      <KpiCard icon={CheckCircle2} title="Confirmados" value={String(confirmed)} hint={share(confirmed)} tone="success" isLoading={isLoading} />
      <KpiCard icon={Clock} title="Pendentes" value={String(pending)} hint={share(pending)} tone="warning" isLoading={isLoading} />
      <KpiCard icon={PlayCircle} title="Em atendimento" value={String(inProgress)} hint={share(inProgress)} tone="info" isLoading={isLoading} />
      <KpiCard
        icon={Wallet}
        title="Faturamento estimado"
        value={formatCurrency(estimatedRevenue)}
        hint="Hoje"
        tone="destructive"
        isLoading={isLoading}
      />
    </div>
  );
}
