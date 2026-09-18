"use client";

import Link from "next/link";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

type Stat = { label: string; value: number | undefined; href: string };

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
  const stats: Stat[] = [
    { label: "Novos clientes", value: newCount, href: "/clientes?segmento=Novo" },
    { label: "Clientes recorrentes", value: recurringCount, href: "/clientes?segmento=Recorrente" },
    { label: "Clientes inativos", value: inactiveCount, href: "/clientes?segmento=Inativo" },
  ];

  return (
    <Card className="ring-0 shadow-sm">
    <CardContent>
      <h3 className="mb-3 text-sm font-semibold">Clientes</h3>
      <div className="grid grid-cols-3 gap-4">
        {stats.map((stat) => (
          <Link key={stat.label} href={stat.href} className="rounded-md p-1 transition-colors hover:bg-accent">
            <p className="text-muted-foreground text-xs">{stat.label}</p>
            {isLoading ? (
              <Skeleton className="mt-1 h-7 w-12" />
            ) : (
              <p className="text-xl font-semibold tabular-nums">{stat.value ?? 0}</p>
            )}
          </Link>
        ))}
      </div>
    </CardContent>
    </Card>
  );
}
