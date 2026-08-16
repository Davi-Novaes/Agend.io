"use client";

import * as React from "react";
import { AlertTriangle, CheckCircle2, Clock, XCircle } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import type { PlatformDashboardMetrics } from "@/lib/api/client";

type ViewMode = "chart" | "table";

type SegmentKey = "trialing" | "active" | "pastDue" | "canceled";

type Segment = {
  key: SegmentKey;
  label: string;
  count: number;
  colorClassName: string;
  icon: typeof CheckCircle2;
};

/**
 * Status carrega significado (ativa = bom, atrasada/cancelada = ruim), entao usa
 * a paleta fixa de status (success/warning/destructive/info) e vira barra
 * horizontal empilhada (parte-todo) — mesmo padrao de AppointmentStatusChart
 * (painel do tenant, Fase 3), so com os 4 status de SubscriptionStatus.
 */
export function SubscriptionStatusChart({ metrics }: { metrics: PlatformDashboardMetrics }) {
  const [view, setView] = React.useState<ViewMode>("chart");
  const [activeSegment, setActiveSegment] = React.useState<SegmentKey | null>(null);

  const segments: Segment[] = [
    { key: "trialing", label: "Em teste gratis", count: metrics.trialingCount, colorClassName: "bg-info", icon: Clock },
    { key: "active", label: "Ativas", count: metrics.activeSubscriptionsCount, colorClassName: "bg-success", icon: CheckCircle2 },
    { key: "pastDue", label: "Pagamento atrasado", count: metrics.pastDueCount, colorClassName: "bg-warning", icon: AlertTriangle },
    { key: "canceled", label: "Canceladas", count: metrics.canceledCount, colorClassName: "bg-destructive", icon: XCircle },
  ];

  const total = segments.reduce((sum, segment) => sum + segment.count, 0);

  function percentOf(count: number): number {
    return total === 0 ? 0 : Math.round((count / total) * 100);
  }

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Assinaturas por status</h3>
          <p className="text-muted-foreground text-xs">Distribuicao de todos os estabelecimentos</p>
        </div>
        <div role="group" aria-label="Alternar visualizacao" className="flex gap-1">
          <Button
            type="button"
            size="sm"
            variant={view === "chart" ? "secondary" : "ghost"}
            aria-pressed={view === "chart"}
            onClick={() => setView("chart")}
          >
            Grafico
          </Button>
          <Button
            type="button"
            size="sm"
            variant={view === "table" ? "secondary" : "ghost"}
            aria-pressed={view === "table"}
            onClick={() => setView("table")}
          >
            Tabela
          </Button>
        </div>
      </div>

      {total === 0 ? (
        <p className="text-muted-foreground py-8 text-center text-sm">Nenhuma assinatura registrada.</p>
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Estabelecimentos</TableHead>
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
        <div>
          <div
            role="img"
            aria-label={`Status de ${total} assinaturas: ${segments
              .map((segment) => `${segment.label} ${percentOf(segment.count)}%`)
              .join(", ")}.`}
            className="flex h-8 w-full gap-0.5 overflow-hidden rounded-md"
          >
            {segments
              .filter((segment) => segment.count > 0)
              .map((segment) => (
                <button
                  key={segment.key}
                  type="button"
                  className={`h-full transition-opacity hover:opacity-80 focus-visible:opacity-80 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 ${segment.colorClassName}`}
                  style={{ width: `${percentOf(segment.count)}%` }}
                  onMouseEnter={() => setActiveSegment(segment.key)}
                  onFocus={() => setActiveSegment(segment.key)}
                  onMouseLeave={() => setActiveSegment(null)}
                  onBlur={() => setActiveSegment(null)}
                  aria-label={`${segment.label}: ${segment.count} (${percentOf(segment.count)}%)`}
                />
              ))}
          </div>
          <div aria-live="polite" className="text-muted-foreground mt-2 h-4 text-xs">
            {activeSegment
              ? (() => {
                  const segment = segments.find((candidate) => candidate.key === activeSegment)!;
                  return `${segment.label}: ${segment.count} (${percentOf(segment.count)}%)`;
                })()
              : ""}
          </div>

          <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1.5" aria-hidden="true">
            {segments.map((segment) => (
              <li key={segment.key} className="flex items-center gap-1.5 text-xs">
                <segment.icon className="size-3.5 shrink-0" aria-hidden="true" />
                <span>
                  {segment.label} ({segment.count})
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
