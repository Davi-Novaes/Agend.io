"use client";

import * as React from "react";
import { useQuery } from "@tanstack/react-query";
import { CalendarCheck, Receipt, Users, Wallet } from "lucide-react";

import { getAppointmentStats, getCashFlowSummary, listCustomers } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { PeriodFilter } from "@/components/shared/period-filter";
import { CategoryBreakdownChart } from "@/components/financeiro/cash-flow-chart";
import { MetricCard } from "@/components/dashboard/metric-card";
import { RevenueChart } from "@/components/dashboard/revenue-chart";
import { AppointmentStatusChart } from "@/components/dashboard/appointment-status-chart";
import { previousPeriod, startOfMonth, toDateOnly } from "@/lib/date-utils";

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function greeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return "Bom dia";
  if (hour < 18) return "Boa tarde";
  return "Boa noite";
}

/** null quando o periodo anterior nao tem base pra comparar (ex.: tenant novo, sem historico). */
function percentDelta(current: number, previous: number): number | null {
  if (previous === 0) return null;
  return ((current - previous) / previous) * 100;
}

export default function DashboardPage() {
  const { session } = useSession();
  const [from, setFrom] = React.useState(() => toDateOnly(startOfMonth(new Date())));
  const [to, setTo] = React.useState(() => toDateOnly(new Date()));
  const previous = previousPeriod(from, to);

  const accessToken = session?.accessToken ?? "";
  const enabled = Boolean(session);

  const cashFlowQuery = useQuery({
    queryKey: ["painel", "financeiro", from, to],
    queryFn: () => getCashFlowSummary({ from, to }, accessToken),
    enabled,
  });
  const previousCashFlowQuery = useQuery({
    queryKey: ["painel", "financeiro", previous.from, previous.to],
    queryFn: () => getCashFlowSummary({ from: previous.from, to: previous.to }, accessToken),
    enabled,
  });
  const appointmentStatsQuery = useQuery({
    queryKey: ["painel", "agenda", from, to],
    queryFn: () => getAppointmentStats({ from, to }, accessToken),
    enabled,
  });
  const previousAppointmentStatsQuery = useQuery({
    queryKey: ["painel", "agenda", previous.from, previous.to],
    queryFn: () => getAppointmentStats({ from: previous.from, to: previous.to }, accessToken),
    enabled,
  });
  const customersQuery = useQuery({
    queryKey: ["painel", "clientes"],
    queryFn: () => listCustomers({ page: 1, pageSize: 1 }, accessToken),
    enabled,
  });

  if (!session) {
    return null;
  }

  const cashFlow = cashFlowQuery.data;
  const previousCashFlow = previousCashFlowQuery.data;
  const stats = appointmentStatsQuery.data;
  const previousStats = previousAppointmentStatsQuery.data;

  const averageTicket = cashFlow && stats && stats.completedCount > 0 ? cashFlow.totalReceived / stats.completedCount : 0;
  const previousAverageTicket =
    previousCashFlow && previousStats && previousStats.completedCount > 0
      ? previousCashFlow.totalReceived / previousStats.completedCount
      : 0;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-1 flex-col">
      <div className="mb-6">
        <h2 className="text-lg font-semibold tracking-tight">{greeting()}</h2>
        <p className="text-muted-foreground text-sm">Veja como esta o desempenho do seu negocio.</p>
      </div>

      <div className="mb-8">
        <PeriodFilter from={from} to={to} onFromChange={setFrom} onToChange={setTo} />
      </div>

      <div className="mb-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          icon={Wallet}
          title="Faturamento"
          value={cashFlow ? formatCurrency(cashFlow.totalReceived) : "—"}
          delta={cashFlow && previousCashFlow ? percentDelta(cashFlow.totalReceived, previousCashFlow.totalReceived) : undefined}
        />
        <MetricCard
          icon={CalendarCheck}
          title="Agendamentos concluidos"
          value={stats ? String(stats.completedCount) : "—"}
          delta={stats && previousStats ? percentDelta(stats.completedCount, previousStats.completedCount) : undefined}
        />
        <MetricCard
          icon={Receipt}
          title="Ticket medio"
          value={cashFlow && stats ? formatCurrency(averageTicket) : "—"}
          delta={
            cashFlow && stats && previousCashFlow && previousStats
              ? percentDelta(averageTicket, previousAverageTicket)
              : undefined
          }
        />
        <MetricCard
          icon={Users}
          title="Total de clientes"
          value={customersQuery.data ? String(customersQuery.data.totalCount) : "—"}
        />
      </div>

      {cashFlow && <RevenueChart data={cashFlow.seriesByMonth} />}

      {stats && (
        <div className="mt-4 grid gap-4 lg:grid-cols-2">
          <AppointmentStatusChart stats={stats} />
          <CategoryBreakdownChart
            id="painel-revenue-by-service"
            title="Faturamento por servico"
            subtitle="Agendamentos concluidos no periodo"
            emptyMessage="Nenhum agendamento concluido no periodo."
            categoryLabel="Servico"
            data={stats.revenueByService.map((point) => ({ category: point.serviceName, total: point.total }))}
          />
        </div>
      )}
    </div>
  );
}
