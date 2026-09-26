"use client";

import * as React from "react";
import { Sparkles } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import type { ServiceRevenuePoint } from "@/lib/api/client";

type ViewMode = "chart" | "table";
type Metric = "count" | "total";

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

/** Ranking de servicos: barra horizontal, hue sequencial unico (--chart-1, mesma convencao de CategoryBreakdownChart). */
export function ServiceRevenueChart({ data }: { data: ServiceRevenuePoint[] }) {
  const [view, setView] = React.useState<ViewMode>("chart");
  const [metric, setMetric] = React.useState<Metric>("total");

  const sorted = React.useMemo(() => [...data].sort((a, b) => b[metric] - a[metric]), [data, metric]);
  const max = Math.max(1, ...sorted.map((point) => point[metric]));
  const colors = ["var(--chart-1)", "var(--chart-3)", "var(--chart-4)", "var(--chart-2)", "var(--chart-5)"];

  function formatMetric(point: ServiceRevenuePoint): string {
    return metric === "total" ? formatCurrency(point.total) : `${point.count} atendimento${point.count === 1 ? "" : "s"}`;
  }

  return (
    <Card className="bg-card border-border/80 ring-0 shadow-sm">
    <CardContent>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Servicos mais vendidos</h3>
          <p className="text-muted-foreground text-xs">Desempenho por servico no periodo selecionado</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div role="group" aria-label="Metrica" className="flex gap-1">
            <Button
              type="button"
              size="sm"
              variant={metric === "count" ? "default" : "ghost"}
              aria-pressed={metric === "count"}
              onClick={() => setMetric("count")}
            >
              Quantidade
            </Button>
            <Button
              type="button"
              size="sm"
              variant={metric === "total" ? "default" : "ghost"}
              aria-pressed={metric === "total"}
              onClick={() => setMetric("total")}
            >
              Faturamento
            </Button>
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
      </div>

      {sorted.length === 0 ? (
        <EmptyState icon={Sparkles} title="Nenhum atendimento concluido no periodo" />
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Servico</TableHead>
              <TableHead className="text-right">Quantidade</TableHead>
              <TableHead className="text-right">Faturamento</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sorted.map((point) => (
              <TableRow key={point.serviceName}>
                <TableCell>{point.serviceName}</TableCell>
                <TableCell className="text-right tabular-nums">{point.count}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(point.total)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <ul className="bg-surface-inset flex flex-col gap-3 rounded-lg p-3 sm:p-4" aria-label="Servicos mais vendidos">
          {sorted.map((point, index) => (
            <li key={point.serviceName} className="grid grid-cols-[1.75rem_minmax(0,1fr)] items-center gap-3">
              <span className="bg-card text-muted-foreground flex size-7 items-center justify-center rounded-md border text-xs font-semibold tabular-nums">
                {index + 1}
              </span>
              <div className="min-w-0">
                <div className="mb-1.5 flex items-center justify-between gap-3 text-xs">
                  <span className="truncate font-medium">{point.serviceName}</span>
                  <span className="shrink-0 font-medium tabular-nums">{formatMetric(point)}</span>
                </div>
                <div className="bg-muted h-2.5 overflow-hidden rounded-full">
                  <div
                    className="h-full rounded-full shadow-[0_0_10px_-2px_currentColor] transition-[width] duration-500"
                    style={{
                      width: `${Math.max(3, (point[metric] / max) * 100)}%`,
                      backgroundColor: colors[index % colors.length],
                      color: colors[index % colors.length],
                    }}
                  />
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </CardContent>
    </Card>
  );
}
