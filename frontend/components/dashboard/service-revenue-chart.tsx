"use client";

import * as React from "react";
import { Sparkles } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
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

  function formatMetric(point: ServiceRevenuePoint): string {
    return metric === "total" ? formatCurrency(point.total) : `${point.count} atendimento${point.count === 1 ? "" : "s"}`;
  }

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Servicos mais vendidos</h3>
          <p className="text-muted-foreground text-xs">Desempenho por servico no periodo selecionado</p>
        </div>
        <div className="flex items-center gap-2">
          <div role="group" aria-label="Metrica" className="flex gap-1">
            <Button
              type="button"
              size="sm"
              variant={metric === "count" ? "secondary" : "ghost"}
              aria-pressed={metric === "count"}
              onClick={() => setMetric("count")}
            >
              Quantidade
            </Button>
            <Button
              type="button"
              size="sm"
              variant={metric === "total" ? "secondary" : "ghost"}
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
        <ul className="flex flex-col gap-2.5" aria-label="Servicos mais vendidos">
          {sorted.map((point) => (
            <li key={point.serviceName} className="flex items-center gap-3">
              <span className="w-28 shrink-0 truncate text-xs">{point.serviceName}</span>
              <div className="bg-muted h-4 flex-1 overflow-hidden rounded-sm">
                <div
                  className="h-full rounded-sm bg-[#2a78d6] dark:bg-[#3987e5]"
                  style={{ width: `${Math.max(3, (point[metric] / max) * 100)}%` }}
                />
              </div>
              <span className="w-24 shrink-0 text-right text-xs tabular-nums">{formatMetric(point)}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
