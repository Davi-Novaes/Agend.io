"use client";

import * as React from "react";
import Link from "next/link";
import { LineChart } from "lucide-react";
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ValueType, NameType } from "recharts/types/component/DefaultTooltipContent";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import type { CashFlowMonthPoint } from "@/lib/api/client";

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatMonthLabel(month: string): string {
  const [year, monthNumber] = month.split("-").map(Number);
  const date = new Date(year, monthNumber - 1, 1);
  return date.toLocaleDateString("pt-BR", { month: "short", year: "2-digit" });
}

type ViewMode = "chart" | "table";

function RevenueTooltip({
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
      <p className="text-muted-foreground">Faturamento: {formatCurrency(Number(value))}</p>
    </div>
  );
}

/** Tendencia ao longo do tempo, serie unica: area chart, hue sequencial (chart-1, validado pela skill dataviz). */
export function RevenueChart({ data }: { data: CashFlowMonthPoint[] }) {
  const [view, setView] = React.useState<ViewMode>("chart");

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Faturamento ao longo do tempo</h3>
          <p className="text-muted-foreground text-xs">Valores recebidos por mes, no periodo selecionado</p>
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

      {data.length === 0 ? (
        <EmptyState
          icon={LineChart}
          title="Sem dados suficientes para este periodo"
          description="Assim que houver movimentacoes, seu desempenho aparece aqui."
          action={
            <Button asChild size="sm" variant="outline">
              <Link href="/financeiro">Ir para financeiro</Link>
            </Button>
          }
        />
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Mes</TableHead>
              <TableHead className="text-right">Faturamento</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.map((point) => (
              <TableRow key={point.month}>
                <TableCell>{formatMonthLabel(point.month)}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(point.received)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <div
          role="img"
          aria-label={`Grafico de area com o faturamento mensal em ${data.length} meses, de ${formatCurrency(
            Math.min(...data.map((point) => point.received))
          )} a ${formatCurrency(Math.max(...data.map((point) => point.received)))}.`}
          className="h-56 w-full"
        >
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="revenue-chart-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--chart-1)" stopOpacity={0.35} />
                  <stop offset="100%" stopColor="var(--chart-1)" stopOpacity={0.03} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} stroke="var(--border)" />
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
              <Tooltip content={(props) => <RevenueTooltip active={props.active} payload={props.payload} label={props.label} />} />
              <Area type="monotone" dataKey="received" stroke="var(--chart-1)" strokeWidth={2} fill="url(#revenue-chart-fill)" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}
