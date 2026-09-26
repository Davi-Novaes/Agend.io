"use client";

import Link from "next/link";
import { Sparkles, UserPlus, UserCheck, UserX } from "lucide-react";
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";

type Segment = {
  key: "new" | "recurring" | "inactive";
  label: string;
  count: number;
  color: string;
  icon: typeof UserPlus;
  href: string;
};

/**
 * Mesmo padrao visual do AppointmentStatusChart (rosca + legenda + total no
 * centro, dentro de um bg-surface-inset) -- pedido explicito do usuario pra
 * o card "Clientes" ficar consistente com "Status dos agendamentos" ao lado.
 */
export function CustomerStatsCard({
  newCount,
  recurringCount,
  inactiveCount,
  isLoading,
}: {
  newCount: number | undefined;
  recurringCount: number | undefined;
  inactiveCount: number | undefined;
  isLoading: boolean;
}) {
  const segments: Segment[] = [
    { key: "new", label: "Novos", count: newCount ?? 0, color: "var(--info)", icon: UserPlus, href: "/clientes?segmento=Novo" },
    {
      key: "recurring",
      label: "Recorrentes",
      count: recurringCount ?? 0,
      color: "var(--success)",
      icon: UserCheck,
      href: "/clientes?segmento=Recorrente",
    },
    {
      key: "inactive",
      label: "Inativos",
      count: inactiveCount ?? 0,
      color: "var(--chart-4)",
      icon: UserX,
      href: "/clientes?segmento=Inativo",
    },
  ];
  const total = segments.reduce((sum, segment) => sum + segment.count, 0);
  const chartData = segments.filter((segment) => segment.count > 0);

  function percentOf(count: number): number {
    return total === 0 ? 0 : Math.round((count / total) * 100);
  }

  // Retencao = dos clientes que ja tiveram pelo menos 1 visita (recorrentes +
  // inativos -- "Novo" ainda nao teve visita concluida nenhuma, nao entra na
  // conta), quantos continuam voltando. Sem base de comparacao (denominador
  // 0) o destaque nao aparece, em vez de mostrar "0%" enganoso.
  const retentionBase = (recurringCount ?? 0) + (inactiveCount ?? 0);
  const retentionRate = retentionBase > 0 ? Math.round(((recurringCount ?? 0) / retentionBase) * 100) : null;

  return (
    <Card className="bg-card border-border/80 ring-0 shadow-sm">
      <CardContent className="@container">
        <div className="mb-3 flex items-center justify-between gap-2">
          <h3 className="text-sm font-semibold">Clientes</h3>
          {!isLoading && retentionRate !== null && (
            <span className="text-muted-foreground flex items-center gap-1.5 text-xs">
              <Sparkles className="text-primary size-3.5" aria-hidden="true" />
              <span className="text-foreground font-medium tabular-nums">{retentionRate}%</span> de retencao
            </span>
          )}
        </div>

        {isLoading ? (
          <div className="flex items-center gap-4 p-3">
            <Skeleton className="size-40 shrink-0 rounded-full" />
            <div className="flex min-w-0 flex-1 flex-col gap-2">
              <Skeleton className="h-5 w-full" />
              <Skeleton className="h-5 w-full" />
              <Skeleton className="h-5 w-full" />
            </div>
          </div>
        ) : total === 0 ? (
          <EmptyState icon={UserPlus} title="Nenhum cliente cadastrado ainda" />
        ) : (
          <div className="bg-surface-inset flex flex-col items-center gap-5 rounded-lg p-4 @sm:flex-row">
            <div
              className="relative size-44 shrink-0 rounded-full ring-1 ring-border/60"
              role="img"
              aria-label={`${total} clientes: ${segments.map((segment) => `${segment.label} ${percentOf(segment.count)}%`).join(", ")}.`}
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
                <span className="text-3xl font-semibold tabular-nums">{total}</span>
                <span className="text-muted-foreground text-[11px]">clientes</span>
              </div>
            </div>

            <ul className="flex min-w-0 flex-1 flex-col gap-1.5" aria-label="Legenda de clientes por segmento">
              {segments.map((segment) => (
                <li key={segment.key}>
                  <Link
                    href={segment.href}
                    className="bg-card/70 hover:bg-card-hover flex items-center gap-2 rounded-md border border-border/50 px-2.5 py-2 text-xs transition-colors"
                  >
                    <span aria-hidden="true" className="inline-block size-2.5 shrink-0 rounded-full" style={{ backgroundColor: segment.color }} />
                    <segment.icon className="text-muted-foreground size-3.5 shrink-0" aria-hidden="true" />
                    <span className="min-w-0 flex-1 truncate">{segment.label}</span>
                    <span className="shrink-0 font-medium tabular-nums">{segment.count}</span>
                    <span className="text-muted-foreground w-9 shrink-0 text-right tabular-nums">{percentOf(segment.count)}%</span>
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
