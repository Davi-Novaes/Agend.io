"use client";

import * as React from "react";
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ValueType, NameType } from "recharts/types/component/DefaultTooltipContent";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import type { ReviewMonthPoint } from "@/lib/api/client";

function formatRating(value: number): string {
  return `${value.toFixed(1)} ★`;
}

function formatMonthLabel(month: string): string {
  const [year, monthNumber] = month.split("-").map(Number);
  const date = new Date(year, monthNumber - 1, 1);
  return date.toLocaleDateString("pt-BR", { month: "short", year: "2-digit" });
}

type ViewMode = "chart" | "table";

function RatingTooltip({
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
      <p className="text-muted-foreground">Media: {formatRating(Number(value))}</p>
    </div>
  );
}

/** Mesmo desenho de RevenueChart (Painel) — clonado de proposito para o dominio 0-5 estrelas, nunca moeda. */
export function RatingEvolutionChart({ data }: { data: ReviewMonthPoint[] }) {
  const [view, setView] = React.useState<ViewMode>("chart");

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Media de avaliacao ao longo do tempo</h3>
          <p className="text-muted-foreground text-xs">Media mensal, no periodo selecionado</p>
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
        <p className="text-muted-foreground py-8 text-center text-sm">Nenhuma avaliacao registrada no periodo.</p>
      ) : view === "table" ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Mes</TableHead>
              <TableHead className="text-right">Media</TableHead>
              <TableHead className="text-right">Avaliacoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.map((point) => (
              <TableRow key={point.month}>
                <TableCell>{formatMonthLabel(point.month)}</TableCell>
                <TableCell className="text-right tabular-nums">{formatRating(point.averageRating)}</TableCell>
                <TableCell className="text-right tabular-nums">{point.count}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <div
          role="img"
          aria-label={`Grafico de area com a media de avaliacao mensal em ${data.length} meses, de ${formatRating(
            Math.min(...data.map((point) => point.averageRating))
          )} a ${formatRating(Math.max(...data.map((point) => point.averageRating)))}.`}
          className="h-56 w-full"
        >
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="rating-chart-fill" x1="0" y1="0" x2="0" y2="1">
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
                domain={[0, 5]}
                tickFormatter={(value: number) => formatRating(value)}
                tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                axisLine={false}
                tickLine={false}
                width={56}
              />
              <Tooltip content={(props) => <RatingTooltip active={props.active} payload={props.payload} label={props.label} />} />
              <Area type="monotone" dataKey="averageRating" stroke="var(--chart-1)" strokeWidth={2} fill="url(#rating-chart-fill)" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}
