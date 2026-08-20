"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Building2, CheckCircle2, TrendingUp, Wallet } from "lucide-react";

import { getPlatformDashboardMetrics } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { AdminNav } from "@/components/platform/admin-nav";
import { MetricCard } from "@/components/dashboard/metric-card";
import { SignupsChart } from "@/components/platform/signups-chart";
import { SubscriptionStatusChart } from "@/components/platform/subscription-status-chart";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";

function formatCurrency(value: number, currency: string): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency });
}

export default function PlatformDashboardPage() {
  const router = useRouter();
  const { session } = usePlatformSession();

  React.useEffect(() => {
    if (!session) {
      router.replace("/admin/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const metricsQuery = useQuery({
    queryKey: ["platform", "dashboard"],
    queryFn: () => getPlatformDashboardMetrics(accessToken),
    enabled: Boolean(session),
  });

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-5xl flex-1 flex-col gap-6 p-6">
      <AdminNav />

      <div>
        <h1 className="text-xl font-semibold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground text-sm">Visao geral do Agend.io como plataforma.</p>
      </div>

      {metricsQuery.isLoading ? (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-24 rounded-xl" />
            ))}
          </div>
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <Skeleton className="h-72 rounded-xl" />
            <Skeleton className="h-72 rounded-xl" />
          </div>
        </div>
      ) : metricsQuery.isError || !metricsQuery.data ? (
        <EmptyState
          icon={AlertTriangle}
          title="Nao foi possivel carregar as metricas"
          description="Tente recarregar a pagina em instantes."
        />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <MetricCard icon={Building2} title="Estabelecimentos" value={String(metricsQuery.data.totalTenants)} />
            <MetricCard icon={CheckCircle2} title="Estabelecimentos ativos" value={String(metricsQuery.data.activeTenants)} />
            <MetricCard icon={TrendingUp} title="Novos este mes" value={String(metricsQuery.data.newTenantsThisMonth)} />
            <MetricCard
              icon={Wallet}
              title="MRR"
              value={formatCurrency(metricsQuery.data.mrr, metricsQuery.data.mrrCurrency)}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <SignupsChart data={metricsQuery.data.newTenantsBySeriesMonth} />
            <SubscriptionStatusChart metrics={metricsQuery.data} />
          </div>
        </>
      )}
    </main>
  );
}
