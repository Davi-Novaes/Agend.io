"use client";

import * as React from "react";
import { LineChart as LineChartIcon } from "lucide-react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { ValueType, NameType } from "recharts/types/component/DefaultTooltipContent";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
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
        variant={view === "chart" ? "default" : "ghost"}
        aria-pressed={view === "chart"}
        onClick={() => onChange("chart")}
        id={`${id}-chart-btn`}
      >
        Grafico
      </Button>
      <Button
        type="button"
        size="sm"
        variant={view === "table" ? "default" : "ghost"}
        aria-pressed={view === "table"}
        onClick={() => onChange("table")}
      >
        Tabela
      </Button>
    </div>
  );
}

function MonthlyEvolutionTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: readonly { dataKey?: string; value?: ValueType }[];
  label?: NameType;
}) {
  if (!active || !payload?.length) return null;
  const received = Number(payload.find((p) => p.dataKey === "received")?.value ?? 0);
  const paid = Number(payload.find((p) => p.dataKey === "paid")?.value ?? 0);

  return (
    <div className="bg-popover text-popover-foreground min-w-40 rounded-lg border px-3.5 py-3 text-xs shadow-lg">
      <p className="font-semibold">{formatMonthLabel(String(label))}</p>
      <p className="mt-1.5 flex items-center justify-between gap-4">
        <span className="flex items-center gap-1.5 text-[color:var(--chart-received)]">
          <span aria-hidden="true" className="inline-block size-2 rounded-full bg-current" />
          Recebido
        </span>
        <span className="font-medium tabular-nums">{formatCurrency(received)}</span>
      </p>
      <p className="mt-1 flex items-center justify-between gap-4">
        <span className="flex items-center gap-1.5 text-[color:var(--chart-paid)]">
          <span aria-hidden="true" className="inline-block size-2 rounded-full bg-current" />
          Pago
        </span>
        <span className="font-medium tabular-nums">{formatCurrency(paid)}</span>
      </p>
      <p className="text-muted-foreground mt-1.5 flex items-center justify-between gap-4 border-t pt-1.5">
        <span>Saldo</span>
        <span className="font-medium tabular-nums">{formatCurrency(received - paid)}</span>
      </p>
    </div>
  );
}

/** Evolucao mensal: recebido x pago, barras agrupadas (Recharts). Cores validadas (verde/vermelho) — ver skill dataviz. */
function MonthlyEvolutionChart({ data }: { data: CashFlowMonthPoint[] }) {
  const [view, setView] = React.useState<ViewMode>("chart");

  return (
    <div className="rounded-xl border p-4 shadow-sm sm:p-5">
      <div className="mb-1 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Evolucao mensal</h3>
          <p className="text-muted-foreground text-xs">Entradas e saidas realizadas, por mes</p>
        </div>
        <ViewToggle view={view} onChange={setView} id="monthly-evolution" />
      </div>

      <div className="mt-3 mb-1 flex items-center gap-4 text-xs">
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="inline-block size-2.5 rounded-full bg-[color:var(--chart-received)]" />
          Recebido
        </span>
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="inline-block size-2.5 rounded-full bg-[color:var(--chart-paid)]" />
          Pago
        </span>
      </div>

      {data.length === 0 ? (
        <EmptyState icon={LineChartIcon} title="Nenhuma movimentacao no periodo" />
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
        <div
          role="img"
          aria-label={`Grafico de barras com a evolucao mensal de recebido e pago em ${data.length} meses.`}
          className="h-64 w-full"
        >
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} barGap={4}>
              <CartesianGrid vertical={false} stroke="var(--border)" strokeOpacity={0.6} strokeDasharray="3 6" />
              <XAxis
                dataKey="month"
                tickFormatter={(value: string) => formatMonthLabel(value)}
                tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <YAxis
                tickFormatter={(value: number) => formatCurrency(value)}
                tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                axisLine={false}
                tickLine={false}
                width={80}
              />
              <Tooltip content={<MonthlyEvolutionTooltip />} cursor={{ fill: "var(--muted)", opacity: 0.4 }} />
              <Bar dataKey="received" name="Recebido" fill="var(--chart-received)" radius={[4, 4, 0, 0]} maxBarSize={28} />
              <Bar dataKey="paid" name="Pago" fill="var(--chart-paid)" radius={[4, 4, 0, 0]} maxBarSize={28} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}

function CategoryTooltip({
  active,
  payload,
  categoryLabel,
  valueFormatter,
}: {
  active?: boolean;
  payload?: readonly { payload: CashFlowCategoryPoint }[];
  categoryLabel: string;
  valueFormatter: (value: number) => string;
}) {
  if (!active || !payload?.[0]) return null;
  const point = payload[0].payload;
  return (
    <div className="bg-popover text-popover-foreground rounded-lg border px-3 py-2 text-xs shadow-lg">
      <p className="font-semibold">{CATEGORY_LABELS[point.category] ?? point.category}</p>
      <p className="text-muted-foreground mt-0.5">
        {categoryLabel}: {valueFormatter(point.total)}
      </p>
    </div>
  );
}

/** Lista generica de categoria/total: barras horizontais (Recharts), hue sequencial unico (ver skill dataviz). */
export function CategoryBreakdownChart({
  data,
  id = "category-breakdown",
  title = "Despesas por categoria",
  subtitle = "Saidas pagas no periodo",
  emptyMessage = "Nenhuma despesa paga no periodo.",
  categoryLabel = "Categoria",
  valueFormatter = formatCurrency,
}: {
  data: CashFlowCategoryPoint[];
  id?: string;
  title?: string;
  subtitle?: string;
  emptyMessage?: string;
  categoryLabel?: string;
  /** Formata o valor da barra/celula — default e moeda (uso original, Financeiro). Outros consumidores (ex.: media de avaliacao) passam o proprio formatador. */
  valueFormatter?: (value: number) => string;
}) {
  const [view, setView] = React.useState<ViewMode>("chart");

  // Recharts layout="vertical" desenha de baixo pra cima -- inverte aqui pra
  // a categoria de maior valor aparecer no topo (leitura natural, mesma
  // ordem que a lista/tabela ja usava).
  const chartData = [...data].reverse();
  // Altura minima por linha, senao poucas categorias ficam com barras enormes
  // e muitas ficam espremidas -- ResponsiveContainer respeita essa altura fixa.
  const chartHeight = Math.max(120, chartData.length * 40);

  return (
    <div className="rounded-xl border p-4 shadow-sm sm:p-5">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">{title}</h3>
          <p className="text-muted-foreground text-xs">{subtitle}</p>
        </div>
        <ViewToggle view={view} onChange={setView} id={id} />
      </div>

      {data.length === 0 ? (
        <EmptyState icon={LineChartIcon} title={emptyMessage} />
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
                <TableCell className="text-right tabular-nums">{valueFormatter(point.total)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <div role="img" aria-label={`Grafico de barras com ${categoryLabel.toLowerCase()} por total.`} style={{ height: chartHeight }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData} layout="vertical" margin={{ top: 4, right: 24, left: 0, bottom: 4 }}>
              <CartesianGrid horizontal={false} stroke="var(--border)" strokeOpacity={0.6} strokeDasharray="3 6" />
              <XAxis type="number" hide />
              <YAxis
                type="category"
                dataKey="category"
                tickFormatter={(value: string) => CATEGORY_LABELS[value] ?? value}
                tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                axisLine={false}
                tickLine={false}
                width={96}
              />
              <Tooltip
                content={<CategoryTooltip categoryLabel={categoryLabel} valueFormatter={valueFormatter} />}
                cursor={{ fill: "var(--muted)", opacity: 0.4 }}
              />
              <Bar
                dataKey="total"
                radius={[0, 4, 4, 0]}
                maxBarSize={22}
                label={{
                  position: "right",
                  fontSize: 11,
                  fill: "var(--muted-foreground)",
                  formatter: (value: unknown) => valueFormatter(Number(value)),
                }}
              >
                {chartData.map((point) => (
                  <Cell key={point.category} fill="var(--chart-1)" />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
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
