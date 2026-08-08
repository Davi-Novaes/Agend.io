"use client";

import * as React from "react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import type { CashFlowCategoryPoint, CashFlowMonthPoint } from "@/lib/api/client";

export const CATEGORY_LABELS: Record<string, string> = {
  Rent: "Aluguel",
  Supplies: "Insumos",
  Commission: "Comissao",
  Salary: "Salario",
  Utilities: "Contas",
  Marketing: "Marketing",
  Other: "Outros",
};

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatMonthLabel(month: string): string {
  const [year, monthNumber] = month.split("-").map(Number);
  const date = new Date(year, monthNumber - 1, 1);
  return date.toLocaleDateString("pt-BR", { month: "short", year: "2-digit" });
}

type ViewMode = "chart" | "table";

function ViewToggle({ view, onChange, id }: { view: ViewMode; onChange: (view: ViewMode) => void; id: string }) {
  return (
    <div role="group" aria-label="Alternar visualizacao" className="flex gap-1">
      <Button
        type="button"
        size="sm"
        variant={view === "chart" ? "secondary" : "ghost"}
        aria-pressed={view === "chart"}
        onClick={() => onChange("chart")}
        id={`${id}-chart-btn`}
      >
        Grafico
      </Button>
      <Button
        type="button"
        size="sm"
        variant={view === "table" ? "secondary" : "ghost"}
        aria-pressed={view === "table"}
        onClick={() => onChange("table")}
      >
        Tabela
      </Button>
    </div>
  );
}

/** Evolucao mensal: recebido x pago, barras agrupadas. Cores validadas (verde/vermelho) — ver skill dataviz. */
function MonthlyEvolutionChart({ data }: { data: CashFlowMonthPoint[] }) {
  const [view, setView] = React.useState<ViewMode>("chart");
  const [activePoint, setActivePoint] = React.useState<{ month: string; series: "Recebido" | "Pago"; value: number } | null>(null);

  const max = Math.max(1, ...data.flatMap((point) => [point.received, point.paid]));

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Evolucao mensal</h3>
          <p className="text-muted-foreground text-xs">Entradas e saidas realizadas, por mes</p>
        </div>
        <ViewToggle view={view} onChange={setView} id="monthly-evolution" />
      </div>

      <div className="mb-3 flex items-center gap-4 text-xs">
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="inline-block size-2.5 rounded-full bg-[#008300]" />
          Recebido
        </span>
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="inline-block size-2.5 rounded-full bg-[#e34948] dark:bg-[#e66767]" />
          Pago
        </span>
      </div>

      {data.length === 0 ? (
        <p className="text-muted-foreground py-8 text-center text-sm">Nenhuma movimentacao no periodo.</p>
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Mes</TableHead>
              <TableHead className="text-right">Recebido</TableHead>
              <TableHead className="text-right">Pago</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.map((point) => (
              <TableRow key={point.month}>
                <TableCell>{formatMonthLabel(point.month)}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(point.received)}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(point.paid)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <div className="relative">
          <div
            role="img"
            aria-label={`Grafico de barras com a evolucao mensal de recebido e pago em ${data.length} meses.`}
            className="flex h-48 items-end gap-4 border-b border-[#c3c2b7] pl-1 dark:border-[#383835]"
          >
            {data.map((point) => (
              <div key={point.month} className="flex flex-1 flex-col items-center gap-1">
                <div className="flex h-40 w-full items-end justify-center gap-[2px]">
                  <button
                    type="button"
                    className="w-full max-w-6 rounded-t-[4px] bg-[#008300] transition-opacity hover:opacity-80 focus-visible:opacity-80 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
                    style={{ height: `${Math.max(2, (point.received / max) * 100)}%` }}
                    onMouseEnter={() => setActivePoint({ month: point.month, series: "Recebido", value: point.received })}
                    onFocus={() => setActivePoint({ month: point.month, series: "Recebido", value: point.received })}
                    onMouseLeave={() => setActivePoint(null)}
                    onBlur={() => setActivePoint(null)}
                    aria-label={`${formatMonthLabel(point.month)}, recebido: ${formatCurrency(point.received)}`}
                  />
                  <button
                    type="button"
                    className="w-full max-w-6 rounded-t-[4px] bg-[#e34948] transition-opacity hover:opacity-80 focus-visible:opacity-80 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 dark:bg-[#e66767]"
                    style={{ height: `${Math.max(2, (point.paid / max) * 100)}%` }}
                    onMouseEnter={() => setActivePoint({ month: point.month, series: "Pago", value: point.paid })}
                    onFocus={() => setActivePoint({ month: point.month, series: "Pago", value: point.paid })}
                    onMouseLeave={() => setActivePoint(null)}
                    onBlur={() => setActivePoint(null)}
                    aria-label={`${formatMonthLabel(point.month)}, pago: ${formatCurrency(point.paid)}`}
                  />
                </div>
                <span className="text-muted-foreground text-[11px]">{formatMonthLabel(point.month)}</span>
              </div>
            ))}
          </div>
          <div aria-live="polite" className="text-muted-foreground mt-2 h-4 text-xs">
            {activePoint
              ? `${formatMonthLabel(activePoint.month)} — ${activePoint.series}: ${formatCurrency(activePoint.value)}`
              : ""}
          </div>
        </div>
      )}
    </div>
  );
}

/** Lista generica de categoria/total: barras horizontais, hue sequencial unico (ver skill dataviz). */
export function CategoryBreakdownChart({
  data,
  id = "category-breakdown",
  title = "Despesas por categoria",
  subtitle = "Saidas pagas no periodo",
  emptyMessage = "Nenhuma despesa paga no periodo.",
  categoryLabel = "Categoria",
}: {
  data: CashFlowCategoryPoint[];
  id?: string;
  title?: string;
  subtitle?: string;
  emptyMessage?: string;
  categoryLabel?: string;
}) {
  const [view, setView] = React.useState<ViewMode>("chart");
  const max = Math.max(1, ...data.map((point) => point.total));

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">{title}</h3>
          <p className="text-muted-foreground text-xs">{subtitle}</p>
        </div>
        <ViewToggle view={view} onChange={setView} id={id} />
      </div>

      {data.length === 0 ? (
        <p className="text-muted-foreground py-8 text-center text-sm">{emptyMessage}</p>
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{categoryLabel}</TableHead>
              <TableHead className="text-right">Total</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.map((point) => (
              <TableRow key={point.category}>
                <TableCell>{CATEGORY_LABELS[point.category] ?? point.category}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(point.total)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <ul className="flex flex-col gap-2.5" aria-label="Despesas por categoria">
          {data.map((point) => (
            <li key={point.category} className="flex items-center gap-3">
              <span className="w-24 shrink-0 truncate text-xs">{CATEGORY_LABELS[point.category] ?? point.category}</span>
              <div className="bg-muted h-4 flex-1 overflow-hidden rounded-sm">
                <div
                  className="h-full rounded-sm bg-[#2a78d6] dark:bg-[#3987e5]"
                  style={{ width: `${Math.max(3, (point.total / max) * 100)}%` }}
                />
              </div>
              <span className="w-24 shrink-0 text-right text-xs tabular-nums">{formatCurrency(point.total)}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export function CashFlowChart({
  seriesByMonth,
  categoryBreakdown,
}: {
  seriesByMonth: CashFlowMonthPoint[];
  categoryBreakdown: CashFlowCategoryPoint[];
}) {
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <MonthlyEvolutionChart data={seriesByMonth} />
      <CategoryBreakdownChart data={categoryBreakdown} />
    </div>
  );
}
