"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { CircleCheck, PackageX, Receipt, UserRoundX, UsersRound } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

type Alert = {
  key: string;
  icon: LucideIcon;
  tone: "critical" | "warning" | "info";
  message: string;
  href: string;
  ctaLabel: string;
};

const TONE_CLASSES: Record<Alert["tone"], string> = {
  critical: "text-destructive",
  warning: "text-warning",
  info: "text-info",
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
      message: `${overduePayablesCount} ${overduePayablesCount === 1 ? "pagamento precisa" : "pagamentos precisam"} de atencao.`,
      href: "/settings/payments",
      ctaLabel: "Ver pagamentos",
    });
  }

  if (pendingAppointmentsCount && pendingAppointmentsCount > 0) {
    alerts.push({
      key: "pending-appointments",
      icon: UserRoundX,
      tone: "warning",
      message: `${pendingAppointmentsCount} ${pendingAppointmentsCount === 1 ? "cliente aguarda" : "clientes aguardam"} confirmacao hoje.`,
      href: "/agenda",
      ctaLabel: "Ver agenda",
    });
  }

  if (lowStockCount && lowStockCount > 0) {
    alerts.push({
      key: "low-stock",
      icon: PackageX,
      tone: "warning",
      message: `${lowStockCount} ${lowStockCount === 1 ? "produto esta" : "produtos estao"} abaixo do estoque minimo.`,
      href: "/estoque",
      ctaLabel: "Ver estoque",
    });
  }

  if (inactiveCustomersCount && inactiveCustomersCount > 0) {
    alerts.push({
      key: "inactive-customers",
      icon: UsersRound,
      tone: "info",
      message: `${inactiveCustomersCount} ${inactiveCustomersCount === 1 ? "cliente nao retorna" : "clientes nao retornam"} ha mais de 90 dias.`,
      href: "/clientes?segmento=Inativo",
      ctaLabel: "Ver clientes",
    });
  }

  return (
    <div>
      <h3 className="mb-3 text-sm font-semibold">Requer sua atencao</h3>
      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : alerts.length === 0 ? (
        <Card size="sm">
          <CardContent className="flex items-center gap-3">
            <CircleCheck className="text-success size-5 shrink-0" aria-hidden="true" />
            <div>
              <p className="text-sm font-medium">Esta tudo em ordem</p>
              <p className="text-muted-foreground text-xs">Nao encontramos nenhuma pendencia importante.</p>
            </div>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          {alerts.map((alert) => (
            <Card key={alert.key} size="sm">
              <CardContent className="flex items-center gap-3">
                <alert.icon className={`size-5 shrink-0 ${TONE_CLASSES[alert.tone]}`} aria-hidden="true" />
                <p className="flex-1 text-sm">{alert.message}</p>
                <Button asChild variant="ghost" size="sm" className="shrink-0">
                  <Link href={alert.href}>{alert.ctaLabel}</Link>
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
