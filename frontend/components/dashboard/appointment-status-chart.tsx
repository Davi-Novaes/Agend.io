"use client";

import * as React from "react";
import Link from "next/link";
import { CalendarCheck, CalendarDays, CheckCircle2, Clock, UserX, XCircle } from "lucide-react";
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import type { AppointmentStats } from "@/lib/api/client";

type ViewMode = "chart" | "table";

type Segment = {
  key: "confirmed" | "scheduled" | "completed" | "cancelled" | "noShow";
  label: string;
  count: number;
  color: string;
  icon: typeof CheckCircle2;
};

/**
 * Status carrega significado (concluido = bom, no-show/cancelado = ruim, os
 * demais neutros) — usa cores fixas por status (--destructive pro cancelado,
 * --chart-N pros demais) em vez da paleta categorica pura, mas em rosca (nao
 * barra) a pedido explicito do usuario: ja tinha sido barra empilhada por
 * causa da skill dataviz, mudou aqui porque a preferencia visual foi reafirmada.
 */
export function AppointmentStatusChart({ stats }: { stats: AppointmentStats }) {
  const [view, setView] = React.useState<ViewMode>("chart");

  const segments: Segment[] = [
    { key: "confirmed", label: "Confirmados", count: stats.confirmedCount, color: "var(--chart-1)", icon: CalendarCheck },
    { key: "scheduled", label: "Pendentes", count: stats.scheduledCount, color: "var(--chart-4)", icon: Clock },
    { key: "completed", label: "Concluidos", count: stats.completedCount, color: "var(--chart-3)", icon: CheckCircle2 },
    { key: "cancelled", label: "Cancelados", count: stats.cancelledCount, color: "var(--destructive)", icon: XCircle },
    { key: "noShow", label: "Nao compareceu", count: stats.noShowCount, color: "var(--chart-2)", icon: UserX },
  ];
  const chartData = segments.filter((segment) => segment.count > 0);

  function percentOf(count: number): number {
    return stats.totalCount === 0 ? 0 : Math.round((count / stats.totalCount) * 100);
  }

  return (
    <Card className="bg-card border-border/80 ring-0 shadow-sm">
    <CardContent className="@container">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Status dos agendamentos</h3>
          <p className="text-muted-foreground text-xs">Distribuicao por status, no periodo selecionado</p>
        </div>
        <div role="group" aria-label="Alternar visualizacao" className="flex gap-1">
          <Button
            type="button"
            size="sm"
            variant={view === "chart" ? "default" : "ghost"}
            aria-pressed={view === "chart"}
            onClick={() => setView("chart")}
          >
            Grafico
          </Button>
          <Button
            type="button"
            size="sm"
            variant={view === "table" ? "default" : "ghost"}
            aria-pressed={view === "table"}
            onClick={() => setView("table")}
          >
            Tabela
          </Button>
        </div>
      </div>

      {stats.totalCount === 0 ? (
        <EmptyState
          icon={CalendarDays}
          title="Sem agendamentos"
          description="Voce ainda nao possui agendamentos neste periodo."
          action={
            <Button asChild size="sm" variant="outline">
              <Link href="/agenda?novo=1">Criar agendamento</Link>
            </Button>
          }
        />
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Agendamentos</TableHead>
              <TableHead className="text-right">%</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {segments.map((segment) => (
              <TableRow key={segment.key}>
                <TableCell>{segment.label}</TableCell>
                <TableCell className="text-right tabular-nums">{segment.count}</TableCell>
                <TableCell className="text-right tabular-nums">{percentOf(segment.count)}%</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <div className="bg-surface-inset flex flex-col items-center gap-5 rounded-lg p-4 @sm:flex-row">
          <div
            className="relative size-44 shrink-0 rounded-full ring-1 ring-border/60"
            role="img"
            aria-label={`Status de ${stats.totalCount} agendamentos: ${segments
              .map((segment) => `${segment.label} ${percentOf(segment.count)}%`)
              .join(", ")}.`}
          >
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={chartData}
                  dataKey="count"
                  nameKey="label"
                  cx="50%"
                  cy="50%"
                  innerRadius={56}
                  outerRadius={82}
                  paddingAngle={3}
                  cornerRadius={5}
                  stroke="none"
                  isAnimationActive={false}
                >
                  {chartData.map((segment) => (
                    <Cell key={segment.key} fill={segment.color} />
                  ))}
                </Pie>
                <Tooltip
                  content={({ active, payload }) => {
                    if (!active || !payload?.[0]) return null;
                    const segment = payload[0].payload as Segment;
                    return (
                      <div className="bg-popover text-popover-foreground rounded-md border px-3 py-2 text-xs shadow-md">
                        {segment.label}: {segment.count} ({percentOf(segment.count)}%)
                      </div>
                    );
                  }}
                />
              </PieChart>
            </ResponsiveContainer>
            <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
              <span className="text-3xl font-semibold tabular-nums">{stats.totalCount}</span>
              <span className="text-muted-foreground text-[11px]">agendamentos</span>
            </div>
          </div>

          <ul className="flex w-full flex-col gap-1.5" aria-label="Legenda de status dos agendamentos">
            {segments.map((segment) => (
              <li key={segment.key} className="bg-card/70 flex items-center gap-2 rounded-md border border-border/50 px-2.5 py-2 text-xs">
                <span aria-hidden="true" className="inline-block size-2.5 shrink-0 rounded-full" style={{ backgroundColor: segment.color }} />
                <segment.icon className="text-muted-foreground size-3.5 shrink-0" aria-hidden="true" />
                <span className="min-w-0 flex-1 truncate">{segment.label}</span>
                <span className="shrink-0 font-medium tabular-nums">{segment.count}</span>
                <span className="text-muted-foreground w-9 shrink-0 text-right tabular-nums">{percentOf(segment.count)}%</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </CardContent>
    </Card>
  );
}
