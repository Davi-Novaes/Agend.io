"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";

import { getCashFlowSummary, getAppointmentStats, getInventorySummary } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { PeriodFilter } from "@/components/shared/period-filter";
import { CashFlowChart, CategoryBreakdownChart } from "@/components/financeiro/cash-flow-chart";
import { toDateOnly, startOfMonth } from "@/lib/date-utils";

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export default function RelatoriosPage() {
  const router = useRouter();
  const { session } = useSession();
  const [from, setFrom] = React.useState(() => toDateOnly(startOfMonth(new Date())));
  const [to, setTo] = React.useState(() => toDateOnly(new Date()));

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-5xl flex-1 flex-col p-6 sm:p-10">
      <Link href="/painel" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <div className="mb-6">
        <h1 className="text-xl font-semibold tracking-tight">Relatorios</h1>
        <p className="text-muted-foreground mt-1 text-sm">Visao consolidada de financeiro, agenda e estoque.</p>
      </div>

      <div className="mb-8">
        <PeriodFilter from={from} to={to} onFromChange={setFrom} onToChange={setTo} />
      </div>

      <div className="flex flex-col gap-10">
        <FinanceiroSection from={from} to={to} accessToken={session.accessToken} />
        <AgendaSection from={from} to={to} accessToken={session.accessToken} />
        <EstoqueSection accessToken={session.accessToken} />
      </div>
    </main>
  );
}

function SectionHeading({ title, description }: { title: string; description: string }) {
  return (
    <div>
      <h2 className="text-lg font-semibold tracking-tight">{title}</h2>
      <p className="text-muted-foreground text-sm">{description}</p>
    </div>
  );
}

function KpiCard({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-muted-foreground text-sm font-normal">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-semibold">{value}</p>
      </CardContent>
    </Card>
  );
}

function FinanceiroSection({ from, to, accessToken }: { from: string; to: string; accessToken: string }) {
  const query = useQuery({
    queryKey: ["relatorios", "financeiro", from, to],
    queryFn: () => getCashFlowSummary({ from, to }, accessToken),
  });
  const summary = query.data;

  return (
    <section className="flex flex-col gap-4">
      <SectionHeading title="Financeiro" description="Entradas, saidas e saldo no periodo selecionado." />
      <div className="grid gap-4 sm:grid-cols-3">
        <KpiCard label="Entradas" value={summary ? formatCurrency(summary.totalReceived) : "—"} />
        <KpiCard label="Saidas" value={summary ? formatCurrency(summary.totalPaid) : "—"} />
        <KpiCard label="Saldo" value={summary ? formatCurrency(summary.netBalance) : "—"} />
      </div>
      {summary && <CashFlowChart seriesByMonth={summary.seriesByMonth} categoryBreakdown={summary.categoryBreakdown} />}
    </section>
  );
}

function AgendaSection({ from, to, accessToken }: { from: string; to: string; accessToken: string }) {
  const query = useQuery({
    queryKey: ["relatorios", "agenda", from, to],
    queryFn: () => getAppointmentStats({ from, to }, accessToken),
  });
  const stats = query.data;

  return (
    <section className="flex flex-col gap-4">
      <SectionHeading title="Agenda" description="Conclusao, no-show e faturamento dos agendamentos no periodo." />
      <div className="grid gap-4 sm:grid-cols-3">
        <KpiCard label="Concluidos" value={stats ? stats.completedCount : "—"} />
        <KpiCard label="Taxa de no-show" value={stats ? `${stats.noShowRate}%` : "—"} />
        <KpiCard label="Taxa de cancelamento" value={stats ? `${stats.cancellationRate}%` : "—"} />
      </div>
      {stats && (
        <div className="grid gap-4 lg:grid-cols-2">
          <CategoryBreakdownChart
            id="revenue-by-service"
            title="Faturamento por servico"
            subtitle="Agendamentos concluidos no periodo"
            emptyMessage="Nenhum agendamento concluido no periodo."
            categoryLabel="Servico"
            data={stats.revenueByService.map((point) => ({ category: point.serviceName, total: point.total }))}
          />
          <CategoryBreakdownChart
            id="revenue-by-professional"
            title="Faturamento por profissional"
            subtitle="Agendamentos concluidos no periodo"
            emptyMessage="Nenhum agendamento concluido no periodo."
            categoryLabel="Profissional"
            data={stats.revenueByProfessional.map((point) => ({ category: point.resourceName, total: point.total }))}
          />
        </div>
      )}
    </section>
  );
}

function EstoqueSection({ accessToken }: { accessToken: string }) {
  const query = useQuery({
    queryKey: ["relatorios", "estoque"],
    queryFn: () => getInventorySummary(accessToken),
  });
  const summary = query.data;

  const totalValueLabel = !summary
    ? "—"
    : summary.totalStockValue.length === 0
      ? formatCurrency(0)
      : summary.totalStockValue.map((entry) => `${formatCurrency(entry.total)} (${entry.currency})`).join(", ");

  return (
    <section className="flex flex-col gap-4">
      <SectionHeading title="Estoque" description="Situacao atual dos produtos, sem recorte por periodo." />
      <div className="grid gap-4 sm:grid-cols-3">
        <KpiCard label="Produtos ativos" value={summary ? summary.activeProductCount : "—"} />
        <KpiCard
          label="Estoque baixo"
          value={
            <span className="flex items-center gap-2">
              {summary ? summary.lowStockCount : "—"}
              {summary && summary.lowStockCount > 0 && <Badge variant="destructive">Atencao</Badge>}
            </span>
          }
        />
        <KpiCard label="Valor total em estoque" value={totalValueLabel} />
      </div>
      {summary && summary.lowStockCount > 0 && (
        <Link href="/estoque" className="text-primary w-fit text-sm underline-offset-4 hover:underline">
          Ver produtos com estoque baixo
        </Link>
      )}
    </section>
  );
}
