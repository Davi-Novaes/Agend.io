"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { Bell, CircleCheck, PackageX, Receipt, UserRoundX, UsersRound } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

type Alert = {
  key: string;
  icon: LucideIcon;
  tone: "critical" | "warning" | "info";
  count: number;
  label: string;
  href: string;
  ctaLabel: string;
};

const TONE_CLASSES: Record<Alert["tone"], string> = {
  critical: "bg-destructive/15 text-destructive",
  warning: "bg-warning/15 text-warning",
  info: "bg-info/15 text-info",
};

export function AttentionSection({
  overduePayablesCount,
  pendingAppointmentsCount,
  lowStockCount,
  inactiveCustomersCount,
  isLoading,
}: {
  overduePayablesCount: number | undefined;
  pendingAppointmentsCount: number | undefined;
  lowStockCount: number | undefined;
  inactiveCustomersCount: number | undefined;
  isLoading: boolean;
}) {
  const alerts: Alert[] = [];

  if (overduePayablesCount && overduePayablesCount > 0) {
    alerts.push({
      key: "overdue-payables",
      icon: Receipt,
      tone: "critical",
      count: overduePayablesCount,
      label: overduePayablesCount === 1 ? "pagamento precisa de atencao" : "pagamentos precisam de atencao",
      href: "/settings/payments",
      ctaLabel: "Ver pagamentos",
    });
  }

  if (pendingAppointmentsCount && pendingAppointmentsCount > 0) {
    alerts.push({
      key: "pending-appointments",
      icon: UserRoundX,
      tone: "warning",
      count: pendingAppointmentsCount,
      label: pendingAppointmentsCount === 1 ? "cliente aguarda confirmacao hoje" : "clientes aguardam confirmacao hoje",
      href: "/agenda",
      ctaLabel: "Ver agenda",
    });
  }

  if (lowStockCount && lowStockCount > 0) {
    alerts.push({
      key: "low-stock",
      icon: PackageX,
      tone: "warning",
      count: lowStockCount,
      label: lowStockCount === 1 ? "produto abaixo do estoque minimo" : "produtos abaixo do estoque minimo",
      href: "/estoque",
      ctaLabel: "Ver estoque",
    });
  }

  if (inactiveCustomersCount && inactiveCustomersCount > 0) {
    alerts.push({
      key: "inactive-customers",
      icon: UsersRound,
      tone: "info",
      count: inactiveCustomersCount,
      label: inactiveCustomersCount === 1 ? "cliente nao retorna ha mais de 90 dias" : "clientes nao retornam ha mais de 90 dias",
      href: "/clientes?segmento=Inativo",
      ctaLabel: "Ver clientes",
    });
  }

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center gap-2">
        <Bell className="text-primary size-4" aria-hidden="true" />
        <div>
          <h3 className="text-sm font-semibold">Requer sua atencao</h3>
          <p className="text-muted-foreground text-xs">Existem itens que precisam da sua acao.</p>
        </div>
      </div>

      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Skeleton className="h-20 w-full" />
          <Skeleton className="h-20 w-full" />
        </div>
      ) : alerts.length === 0 ? (
        <div className="flex items-center gap-3 py-6">
          <CircleCheck className="text-success size-5 shrink-0" aria-hidden="true" />
          <div>
            <p className="text-sm font-medium">Esta tudo em ordem</p>
            <p className="text-muted-foreground text-xs">Nao encontramos nenhuma pendencia importante.</p>
          </div>
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          {alerts.map((alert) => (
            <Link
              key={alert.key}
              href={alert.href}
              className="hover:border-primary/40 hover:bg-accent/50 flex flex-col gap-2 rounded-lg border p-3 transition-colors"
            >
              <div className="flex items-center gap-2">
                <div className={cn("flex size-7 items-center justify-center rounded-md", TONE_CLASSES[alert.tone])}>
                  <alert.icon className="size-4" aria-hidden="true" />
                </div>
                <span className="text-lg font-semibold tabular-nums">{alert.count}</span>
              </div>
              <p className="text-muted-foreground text-xs leading-snug">{alert.label}</p>
              <span className="text-primary text-xs font-medium">{alert.ctaLabel} →</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
