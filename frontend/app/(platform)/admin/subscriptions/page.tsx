"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";

import { listSubscriptionsForPlatform } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";

const STATUS_LABELS: Record<string, string> = {
  Trialing: "Em teste gratis",
  Active: "Ativa",
  PastDue: "Pagamento atrasado",
  Canceled: "Cancelada",
};

function statusVariant(status: string): "default" | "secondary" | "destructive" {
  if (status === "Active") return "default";
  if (status === "PastDue") return "destructive";
  return "secondary";
}

export default function PlatformSubscriptionsPage() {
  const router = useRouter();
  const { session } = usePlatformSession();

  React.useEffect(() => {
    if (!session) {
      router.replace("/admin/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const subscriptionsQuery = useQuery({
    queryKey: ["platform", "subscriptions"],
    queryFn: () => listSubscriptionsForPlatform(accessToken),
    enabled: Boolean(session),
  });

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-4xl flex-1 flex-col gap-6 p-6">
      <header>
        <Link href="/admin" className="text-muted-foreground mb-4 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
          <ArrowLeft className="size-4" />
          Voltar
        </Link>
        <h1 className="text-xl font-semibold tracking-tight">Assinaturas</h1>
        <p className="text-muted-foreground text-sm">Status de cobranca de todos os estabelecimentos.</p>
      </header>

      {subscriptionsQuery.isLoading ? (
        <p className="text-muted-foreground text-sm">Carregando...</p>
      ) : subscriptionsQuery.isError ? (
        <p className="text-destructive text-sm">Nao foi possivel carregar as assinaturas.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Estabelecimento</TableHead>
              <TableHead>Plano</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Fim do trial</TableHead>
              <TableHead>Proximo vencimento</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {subscriptionsQuery.data?.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground text-center">
                  Nenhuma assinatura registrada.
                </TableCell>
              </TableRow>
            ) : (
              subscriptionsQuery.data?.map((subscription) => (
                <TableRow key={subscription.tenantId}>
                  <TableCell className="font-medium">{subscription.tenantName}</TableCell>
                  <TableCell>{subscription.planName}</TableCell>
                  <TableCell>
                    <Badge variant={statusVariant(subscription.status)}>
                      {STATUS_LABELS[subscription.status] ?? subscription.status}
                    </Badge>
                  </TableCell>
                  <TableCell>{new Date(subscription.trialEndsAtUtc).toLocaleDateString("pt-BR")}</TableCell>
                  <TableCell>
                    {subscription.currentPeriodEndsAtUtc
                      ? new Date(subscription.currentPeriodEndsAtUtc).toLocaleDateString("pt-BR")
                      : "—"}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      )}

      <div>
        <Button variant="outline" asChild>
          <Link href="/admin">Estabelecimentos</Link>
        </Button>
      </div>
    </main>
  );
}
