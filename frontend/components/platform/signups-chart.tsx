"use client";

import * as React from "react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ValueType, NameType } from "recharts/types/component/DefaultTooltipContent";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import type { SignupMonthPoint } from "@/lib/api/client";

function formatMonthLabel(month: string): string {
  const [year, monthNumber] = month.split("-").map(Number);
  const date = new Date(year, monthNumber - 1, 1);
  return date.toLocaleDateString("pt-BR", { month: "short", year: "2-digit" });
}

type ViewMode = "chart" | "table";

function SignupsTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: readonly { value?: ValueType }[];
  label?: NameType;
}) {
  const value = payload?.[0]?.value;
  if (!active || value === undefined) {
    return null;
  }
  return (
    <div className="bg-popover text-popover-foreground rounded-md border px-3 py-2 text-xs shadow-md">
      <p className="font-medium">{formatMonthLabel(String(label))}</p>
      <p className="text-muted-foreground">Novos estabelecimentos: {value}</p>
    </div>
  );
}

/** Tendencia ao longo do tempo, serie unica: mesmo hue sequencial (chart-1) do RevenueChart do painel do tenant. */
export function SignupsChart({ data }: { data: SignupMonthPoint[] }) {
  const [view, setView] = React.useState<ViewMode>("chart");
  const gradientId = React.useId().replaceAll(":", "");
  const total = data.reduce((sum, point) => sum + point.count, 0);
  const average = data.length > 0 ? total / data.length : 0;

  return (
    <div className="bg-card rounded-xl border border-border/80 p-4 shadow-sm sm:p-5">
      <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold">Novos estabelecimentos</h3>
          <p className="text-muted-foreground text-xs">Cadastros por mes, ultimos 6 meses</p>
          {total > 0 && (
            <p className="mt-2 text-xs">
              <span className="font-semibold tabular-nums">{total} no período</span>
              <span className="text-muted-foreground"> · média de {average.toLocaleString("pt-BR", { maximumFractionDigits: 1 })}/mês</span>
            </p>
          )}
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

      {total === 0 ? (
        <p className="text-muted-foreground py-8 text-center text-sm">Nenhum estabelecimento cadastrado no periodo.</p>
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Mes</TableHead>
              <TableHead className="text-right">Novos estabelecimentos</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.map((point) => (
              <TableRow key={point.month}>
                <TableCell>{formatMonthLabel(point.month)}</TableCell>
                <TableCell className="text-right tabular-nums">{point.count}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <div
          role="img"
          aria-label={`Grafico de barras com novos estabelecimentos por mes, total de ${total} nos ultimos ${data.length} meses.`}
          className="bg-surface-inset h-64 w-full rounded-lg p-3"
        >
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} margin={{ top: 12, right: 8, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id={`${gradientId}-fill`} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--primary)" stopOpacity={1} />
                  <stop offset="100%" stopColor="var(--chart-1)" stopOpacity={0.55} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} stroke="var(--border)" strokeOpacity={0.55} strokeDasharray="3 6" />
              <XAxis
                dataKey="month"
                tickFormatter={(value: string) => formatMonthLabel(value)}
                tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                axisLine={{ stroke: "var(--border)" }}
                tickLine={false}
              />
              <YAxis
                allowDecimals={false}
                tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                axisLine={false}
                tickLine={false}
                width={32}
              />
              <Tooltip content={(props) => <SignupsTooltip active={props.active} payload={props.payload} label={props.label} />} />
              <Bar dataKey="count" fill={`url(#${gradientId}-fill)`} radius={[7, 7, 2, 2]} maxBarSize={42} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}
