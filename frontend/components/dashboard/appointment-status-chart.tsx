"use client";

import * as React from "react";
import Link from "next/link";
import { CalendarCheck, CalendarDays, CheckCircle2, Clock, UserX, XCircle } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import type { AppointmentStats } from "@/lib/api/client";

type ViewMode = "chart" | "table";

type Segment = {
  key: "confirmed" | "scheduled" | "completed" | "cancelled" | "noShow";
  label: string;
  count: number;
  colorClassName: string;
  icon: typeof CheckCircle2;
};

/**
 * Status carrega significado de bom/ruim (concluido = bom, no-show/cancelado = ruim), entao
 * usa a paleta fixa de status da skill dataviz (good/critical), nao a categorica — e vira
 * barra horizontal empilhada (parte-todo), nunca rosca/pizza. Ver references/choosing-a-form.md
 * e references/color-formula.md da skill.
 */
export function AppointmentStatusChart({ stats }: { stats: AppointmentStats }) {
  const [view, setView] = React.useState<ViewMode>("chart");
  const [activeSegment, setActiveSegment] = React.useState<Segment["key"] | null>(null);

  const segments: Segment[] = [
    { key: "confirmed", label: "Confirmados", count: stats.confirmedCount, colorClassName: "bg-[#2a78d6] dark:bg-[#3987e5]", icon: CalendarCheck },
    {
      key: "scheduled",
      label: "Pendentes",
      count: stats.scheduledCount,
      colorClassName: "bg-[#c3c2b7] dark:bg-[#383835]",
      icon: Clock,
    },
    { key: "completed", label: "Concluidos", count: stats.completedCount, colorClassName: "bg-[#0ca30c]", icon: CheckCircle2 },
    { key: "cancelled", label: "Cancelados", count: stats.cancelledCount, colorClassName: "bg-[#d03b3b]", icon: XCircle },
    { key: "noShow", label: "Nao compareceu", count: stats.noShowCount, colorClassName: "bg-[#a5321f] dark:bg-[#e8674d]", icon: UserX },
  ];

  function percentOf(count: number): number {
    return stats.totalCount === 0 ? 0 : Math.round((count / stats.totalCount) * 100);
  }

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Status dos agendamentos</h3>
          <p className="text-muted-foreground text-xs">Distribuicao por status, no periodo selecionado</p>
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
        <div>
          <div
            role="img"
            aria-label={`Status de ${stats.totalCount} agendamentos: ${segments
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
