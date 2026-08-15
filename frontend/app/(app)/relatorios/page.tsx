"use client";

import * as React from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";

import { getCashFlowSummary, getAppointmentStats, getInventorySummary, getReviewsSummary } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { PeriodFilter } from "@/components/shared/period-filter";
import { CashFlowChart, CategoryBreakdownChart } from "@/components/financeiro/cash-flow-chart";
import { RatingEvolutionChart } from "@/components/scheduling/rating-evolution-chart";
import { StarRating } from "@/components/scheduling/star-rating";
import { toDateOnly, startOfMonth } from "@/lib/date-utils";

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export default function RelatoriosPage() {
  const { session } = useSession();
  const [from, setFrom] = React.useState(() => toDateOnly(startOfMonth(new Date())));
  const [to, setTo] = React.useState(() => toDateOnly(new Date()));

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-1 flex-col">
      <div className="mb-6">
        <p className="text-muted-foreground text-sm">Visao consolidada de financeiro, agenda e estoque.</p>
      </div>

      <div className="mb-8">
        <PeriodFilter from={from} to={to} onFromChange={setFrom} onToChange={setTo} />
      </div>

      <div className="flex flex-col gap-10">
        <FinanceiroSection from={from} to={to} accessToken={session.accessToken} />
        <AgendaSection from={from} to={to} accessToken={session.accessToken} />
        <AvaliacoesSection from={from} to={to} accessToken={session.accessToken} />
        <EstoqueSection accessToken={session.accessToken} />
      </div>
    </div>
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

function KpiCard({ label, value, isLoading }: { label: string; value: React.ReactNode; isLoading?: boolean }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-muted-foreground text-sm font-normal">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? <Skeleton className="h-8 w-24" /> : <p className="text-2xl font-semibold">{value}</p>}
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
        <KpiCard label="Entradas" value={summary ? formatCurrency(summary.totalReceived) : "—"} isLoading={query.isLoading} />
        <KpiCard label="Saidas" value={summary ? formatCurrency(summary.totalPaid) : "—"} isLoading={query.isLoading} />
        <KpiCard label="Saldo" value={summary ? formatCurrency(summary.netBalance) : "—"} isLoading={query.isLoading} />
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
        <KpiCard label="Concluidos" value={stats ? stats.completedCount : "—"} isLoading={query.isLoading} />
        <KpiCard label="Taxa de no-show" value={stats ? `${stats.noShowRate}%` : "—"} isLoading={query.isLoading} />
        <KpiCard label="Taxa de cancelamento" value={stats ? `${stats.cancellationRate}%` : "—"} isLoading={query.isLoading} />
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

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString("pt-BR");
}

function AvaliacoesSection({ from, to, accessToken }: { from: string; to: string; accessToken: string }) {
  const query = useQuery({
    queryKey: ["relatorios", "avaliacoes", from, to],
    queryFn: () => getReviewsSummary({ from, to }, accessToken),
  });
  const summary = query.data;

  return (
    <section className="flex flex-col gap-4">
      <SectionHeading title="Avaliacoes" description="Nota media, evolucao e comentarios recentes dos clientes no periodo." />
      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-muted-foreground text-sm font-normal">Media geral</CardTitle>
          </CardHeader>
          <CardContent>
            {query.isLoading ? (
              <Skeleton className="h-8 w-24" />
            ) : summary && summary.totalCount > 0 ? (
              <div className="flex items-center gap-2">
                <span className="text-2xl font-semibold">{summary.averageRating.toFixed(1)}</span>
                <StarRating value={summary.averageRating} readOnly size="sm" />
              </div>
            ) : (
              <p className="text-2xl font-semibold">—</p>
            )}
          </CardContent>
        </Card>
        <KpiCard label="Total de avaliacoes" value={summary ? summary.totalCount : "—"} isLoading={query.isLoading} />
      </div>

      {summary && summary.totalCount > 0 && (
        <>
          <RatingEvolutionChart data={summary.seriesByMonth} />
          <div className="grid gap-4 lg:grid-cols-2">
            <CategoryBreakdownChart
              id="rating-by-service"
              title="Media por servico"
              subtitle="Avaliacoes no periodo"
              emptyMessage="Nenhuma avaliacao no periodo."
              categoryLabel="Servico"
              valueFormatter={(value) => `${value.toFixed(1)} ★`}
              data={summary.byService.map((point) => ({ category: point.serviceName, total: point.averageRating }))}
            />
            <CategoryBreakdownChart
              id="rating-by-professional"
              title="Media por profissional"
              subtitle="Avaliacoes no periodo"
              emptyMessage="Nenhuma avaliacao no periodo."
              categoryLabel="Profissional"
              valueFormatter={(value) => `${value.toFixed(1)} ★`}
              data={summary.byProfessional.map((point) => ({ category: point.resourceName, total: point.averageRating }))}
            />
          </div>

          {summary.recentReviews.length > 0 && (
            <div className="flex flex-col gap-3">
              <h3 className="text-sm font-semibold">Avaliacoes recentes</h3>
              <div className="flex flex-col gap-3">
                {summary.recentReviews.map((review) => (
                  <div key={review.id} className="rounded-lg border border-border p-3">
                    <div className="flex items-center justify-between gap-2">
                      <StarRating value={review.rating} readOnly size="sm" />
                      <span className="text-muted-foreground text-xs">{formatDateTime(review.createdAtUtc)}</span>
                    </div>
                    <p className="text-muted-foreground mt-1 text-xs">{review.serviceName}</p>
                    {review.comment && <p className="mt-1 text-sm">{review.comment}</p>}
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
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
        <KpiCard label="Produtos ativos" value={summary ? summary.activeProductCount : "—"} isLoading={query.isLoading} />
        <KpiCard
          label="Estoque baixo"
          isLoading={query.isLoading}
          value={
            <span className="flex items-center gap-2">
              {summary ? summary.lowStockCount : "—"}
              {summary && summary.lowStockCount > 0 && <Badge variant="destructive">Atencao</Badge>}
            </span>
          }
        />
        <KpiCard label="Valor total em estoque" value={totalValueLabel} isLoading={query.isLoading} />
      </div>
      {summary && summary.lowStockCount > 0 && (
        <Link href="/estoque" className="text-primary w-fit text-sm underline-offset-4 hover:underline">
          Ver produtos com estoque baixo
        </Link>
      )}
    </section>
  );
}
