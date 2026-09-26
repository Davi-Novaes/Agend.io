"use client";

import * as React from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowDownCircle, CalendarCheck, Plus, Scale, Wallet } from "lucide-react";

import {
  getAppointmentStats,
  getCashFlowSummary,
  getCustomerRecoveryCandidates,
  getInventorySummary,
  listAccountsPayable,
  listAppointments,
  listCustomers,
  listResources,
  listServices,
  CUSTOMER_RECOVERY_QUERY_KEY,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtFullName } from "@/lib/auth/decode-jwt";
import { PeriodFilter } from "@/components/shared/period-filter";
import { MetricCard } from "@/components/dashboard/metric-card";
import { RevenueChart } from "@/components/dashboard/revenue-chart";
import { AppointmentStatusChart } from "@/components/dashboard/appointment-status-chart";
import { ServiceRevenueChart } from "@/components/dashboard/service-revenue-chart";
import { TodayAgendaCard, type TodayAppointmentItem } from "@/components/dashboard/today-agenda-card";
import { AttentionSection } from "@/components/dashboard/attention-section";
import { CustomerStatsCard } from "@/components/dashboard/customer-stats-card";
import { InsightsSection } from "@/components/dashboard/insights-section";
import { OnboardingChecklistCard } from "@/components/dashboard/onboarding-checklist-card";
import { Button } from "@/components/ui/button";
import { previousPeriod, startOfYear, toDateOnly } from "@/lib/date-utils";

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function greeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return "Bom dia";
  if (hour < 18) return "Boa tarde";
  return "Boa noite";
}

function firstNameOf(fullName: string | null): string | null {
  return fullName?.trim().split(/\s+/)[0] ?? null;
}

/** null quando o periodo anterior nao tem base pra comparar (ex.: tenant novo, sem historico). */
function percentDelta(current: number, previous: number): number | null {
  if (previous === 0) return null;
  return ((current - previous) / previous) * 100;
}

function addDays(date: Date, days: number): Date {
  const result = new Date(date);
  result.setDate(result.getDate() + days);
  return result;
}

function startOfDay(date: Date): Date {
  const result = new Date(date);
  result.setHours(0, 0, 0, 0);
  return result;
}

export default function DashboardPage() {
  const { session } = useSession();
  // Ano todo por padrao (nao so o mes atual) -- com poucos meses de historico
  // acumulado, "este mes" sozinho deixava o grafico de faturamento parecendo
  // vazio (um unico ponto). O seletor continua permitindo qualquer periodo.
  const [from, setFrom] = React.useState(() => toDateOnly(startOfYear(new Date())));
  const [to, setTo] = React.useState(() => toDateOnly(new Date()));
  const previous = previousPeriod(from, to);

  const accessToken = session?.accessToken ?? "";
  const enabled = Boolean(session);

  const todayStart = React.useMemo(() => startOfDay(new Date()), []);
  const todayEnd = React.useMemo(() => addDays(todayStart, 1), [todayStart]);
  const todayIso = toDateOnly(todayStart);

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
  const recoveryQuery = useQuery({
    queryKey: CUSTOMER_RECOVERY_QUERY_KEY,
    queryFn: () => getCustomerRecoveryCandidates(accessToken),
    enabled,
  });

  const todayAppointmentsQuery = useQuery({
    queryKey: ["painel", "agenda-hoje", todayIso],
    queryFn: () => listAppointments({ fromUtc: todayStart.toISOString(), toUtc: todayEnd.toISOString() }, accessToken),
    enabled,
  });
  const customersForJoinQuery = useQuery({
    queryKey: ["painel", "clientes-join"],
    queryFn: () => listCustomers({ page: 1, pageSize: 100 }, accessToken),
    enabled,
  });

  const overduePayablesQuery = useQuery({
    queryKey: ["painel", "contas-a-pagar-pendentes"],
    queryFn: () => listAccountsPayable({ status: "Pending", pageSize: 100 }, accessToken),
    enabled,
  });
  const lowStockQuery = useQuery({
    queryKey: ["painel", "estoque-resumo"],
    queryFn: () => getInventorySummary(accessToken),
    enabled,
  });
  const newCustomersQuery = useQuery({
    queryKey: ["painel", "clientes-segmento", "Novo"],
    queryFn: () => listCustomers({ page: 1, pageSize: 1, segment: "Novo" }, accessToken),
    enabled,
  });
  const recurringCustomersQuery = useQuery({
    queryKey: ["painel", "clientes-segmento", "Recorrente"],
    queryFn: () => listCustomers({ page: 1, pageSize: 1, segment: "Recorrente" }, accessToken),
    enabled,
  });
  const inactiveCustomersQuery = useQuery({
    queryKey: ["painel", "clientes-segmento", "Inativo"],
    queryFn: () => listCustomers({ page: 1, pageSize: 1, segment: "Inativo" }, accessToken),
    enabled,
  });
  const servicesCountQuery = useQuery({
    queryKey: ["painel", "servicos-count"],
    queryFn: () => listServices({ page: 1, pageSize: 1 }, accessToken),
    enabled,
  });
  const resourcesCountQuery = useQuery({
    queryKey: ["painel", "recursos-count"],
    queryFn: () => listResources({ page: 1, pageSize: 1 }, accessToken),
    enabled,
  });

  if (!session) {
    return null;
  }

  const cashFlow = cashFlowQuery.data;
  const previousCashFlow = previousCashFlowQuery.data;
  const stats = appointmentStatsQuery.data;
  const kpiLoading = cashFlowQuery.isLoading || appointmentStatsQuery.isLoading;

  const customerNameById = new Map((customersForJoinQuery.data?.items ?? []).map((customer) => [customer.id, customer.fullName]));
  const todayAppointments: TodayAppointmentItem[] | undefined = todayAppointmentsQuery.data
    ?.slice()
    .sort((a, b) => a.startUtc.localeCompare(b.startUtc))
    .map((appointment) => ({
      id: appointment.id,
      startUtc: appointment.startUtc,
      customerName: customerNameById.get(appointment.customerId) ?? "Cliente",
      serviceName: appointment.serviceName,
      price: appointment.price,
      currency: appointment.currency,
      status: appointment.status,
    }));

  const todayCompletedCount = todayAppointmentsQuery.data?.filter((a) => a.status === "Completed").length ?? 0;
  const todayPendingCount = todayAppointmentsQuery.data?.filter((a) => a.status === "Scheduled").length ?? 0;

  const overduePayablesCount = overduePayablesQuery.data?.items.filter((item) => item.dueDate < todayIso).length;
  const displayName = firstNameOf(session ? decodeJwtFullName(session.accessToken) : null);

  return (
    <div className="flex w-full flex-1 flex-col gap-6">
      {/* Cabecalho compacto: saudacao + acoes rapidas numa linha, filtro de
          periodo integrado logo abaixo (nao mais numa secao propria) */}
      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold tracking-tight">
              {greeting()}
              {displayName ? `, ${displayName}` : ""} <span aria-hidden="true">👋</span>
            </h2>
            <p className="text-muted-foreground text-sm">Aqui esta o resumo do seu negocio.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              asChild
              variant="ghost"
              size="sm"
              className="border border-primary/50 bg-transparent text-primary hover:bg-primary/10 hover:text-primary dark:border-primary/50 dark:bg-transparent dark:hover:bg-primary/10 dark:hover:text-primary"
            >
              <Link href="/clientes?novo=1">
                <Plus className="size-4" />
                Novo cliente
              </Link>
            </Button>
            <Button asChild size="sm">
              <Link href="/agenda?novo=1">
                <Plus className="size-4" />
                Novo agendamento
              </Link>
            </Button>
          </div>
        </div>
        <PeriodFilter from={from} to={to} onFromChange={setFrom} onToChange={setTo} />
      </div>

      {servicesCountQuery.data && resourcesCountQuery.data && (
        <OnboardingChecklistCard
          servicesCount={servicesCountQuery.data.totalCount}
          resourcesCount={resourcesCountQuery.data.totalCount}
          hasAppointments={(stats?.totalCount ?? 0) > 0}
        />
      )}

      {/* 1. Operacao do dia: vem antes do historico financeiro porque e a
          informacao que orienta a proxima acao de quem acabou de entrar. */}
      <TodayAgendaCard appointments={todayAppointments} isLoading={todayAppointmentsQuery.isLoading} />

      {/* 2. Indicadores principais */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricCard
          icon={Wallet}
          title="Faturamento"
          value={cashFlow ? formatCurrency(cashFlow.totalReceived) : "—"}
          delta={cashFlow && previousCashFlow ? percentDelta(cashFlow.totalReceived, previousCashFlow.totalReceived) : undefined}
          emptyLabel={cashFlow?.totalReceived === 0 ? "Aguardando primeiros lancamentos" : undefined}
          isLoading={kpiLoading}
          tone="primary"
          featured
        />
        <MetricCard
          icon={ArrowDownCircle}
          title="Despesas"
          value={cashFlow ? formatCurrency(cashFlow.totalPaid) : "—"}
          delta={cashFlow && previousCashFlow ? percentDelta(cashFlow.totalPaid, previousCashFlow.totalPaid) : undefined}
          emptyLabel={cashFlow?.totalPaid === 0 ? "Nenhuma despesa lancada ainda" : undefined}
          isLoading={kpiLoading}
          tone="destructive"
        />
        <MetricCard
          icon={Scale}
          title="Resultado"
          value={cashFlow ? formatCurrency(cashFlow.netBalance) : "—"}
          delta={cashFlow && previousCashFlow ? percentDelta(cashFlow.netBalance, previousCashFlow.netBalance) : undefined}
          description="Receitas menos despesas no periodo."
          isLoading={kpiLoading}
          tone="success"
        />
        <MetricCard
          icon={CalendarCheck}
          title="Agendamentos"
          value={todayAppointmentsQuery.data ? `${todayAppointmentsQuery.data.length}` : "—"}
          description={
            todayAppointmentsQuery.data && todayAppointmentsQuery.data.length > 0
              ? `${todayCompletedCount} concluidos hoje`
              : undefined
          }
          emptyLabel={todayAppointmentsQuery.data?.length === 0 ? "Nenhum agendamento hoje" : undefined}
          isLoading={todayAppointmentsQuery.isLoading}
          tone="info"
        />
      </div>

      {/* 3. Faturamento / indicadores financeiros */}
      {cashFlow ? (
        <RevenueChart data={cashFlow.seriesByMonth} />
      ) : (
        <div className="h-64 animate-pulse rounded-lg bg-muted/40" />
      )}

      {/* 4. Alertas e pendencias + 5. Insights do periodo */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <AttentionSection
          overduePayablesCount={overduePayablesCount}
          pendingAppointmentsCount={todayPendingCount}
          lowStockCount={lowStockQuery.data?.lowStockCount}
          inactiveCustomersCount={inactiveCustomersQuery.data?.totalCount}
          isLoading={overduePayablesQuery.isLoading || lowStockQuery.isLoading || inactiveCustomersQuery.isLoading}
        />
        <InsightsSection
          cashFlow={cashFlow}
          previousCashFlow={previousCashFlow}
          stats={stats}
          recoveryCandidates={recoveryQuery.data}
        />
      </div>

      {/* xl, nao lg: ServiceRevenueChart/AppointmentStatusChart tem controles
          internos (toggles, legenda) que dependem de largura de viewport, nao
          da largura da coluna do grid -- em 3 colunas antes de xl eles ficam
          espremidos mesmo em telas largas, porque o breakpoint interno nao
          sabe que ganhou menos espaco por causa do grid pai. */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {stats ? <ServiceRevenueChart data={stats.revenueByService} /> : <div className="h-72 animate-pulse rounded-lg bg-muted/40" />}
        <CustomerStatsCard
          newCount={newCustomersQuery.data?.totalCount}
          recurringCount={recurringCustomersQuery.data?.totalCount}
          inactiveCount={inactiveCustomersQuery.data?.totalCount}
          isLoading={newCustomersQuery.isLoading || recurringCustomersQuery.isLoading || inactiveCustomersQuery.isLoading}
        />
        {stats ? (
          <AppointmentStatusChart stats={stats} />
        ) : (
          <div className="h-72 animate-pulse rounded-lg bg-muted/40" />
        )}
      </div>
    </div>
  );
}
