"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { listSubscriptionsForPlatform, cancelSubscriptionForTenant, ApiError, type SubscriptionAdminSummary } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { AdminNav } from "@/components/platform/admin-nav";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";

const STATUS_LABELS: Record<string, string> = {
  Trialing: "Em teste gratis",
  Active: "Ativa",
  PastDue: "Pagamento atrasado",
  Canceled: "Cancelada",
};

const STATUS_FILTERS = ["all", "Trialing", "Active", "PastDue", "Canceled"] as const;
type StatusFilter = (typeof STATUS_FILTERS)[number];

function statusVariant(status: string): "default" | "secondary" | "destructive" {
  if (status === "Active") return "default";
  if (status === "PastDue") return "destructive";
  return "secondary";
}

export default function PlatformSubscriptionsPage() {
  const router = useRouter();
  const { session } = usePlatformSession();
  const queryClient = useQueryClient();

  const [search, setSearch] = React.useState("");
  const [statusFilter, setStatusFilter] = React.useState<StatusFilter>("all");
  const [cancelingSubscription, setCancelingSubscription] = React.useState<SubscriptionAdminSummary | null>(null);

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

  const cancelMutation = useMutation({
    mutationFn: (tenantId: string) => cancelSubscriptionForTenant(tenantId, accessToken),
    onSuccess: () => {
      toast.success("Assinatura cancelada.");
      queryClient.invalidateQueries({ queryKey: ["platform", "subscriptions"] });
      setCancelingSubscription(null);
    },
    onError: (error: unknown) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cancelar a assinatura.");
      setCancelingSubscription(null);
    },
  });

  if (!session) {
    return null;
  }

  const filteredSubscriptions = (subscriptionsQuery.data ?? []).filter((subscription) => {
    const matchesStatus = statusFilter === "all" || subscription.status === statusFilter;
    const matchesSearch = subscription.tenantName.toLowerCase().includes(search.trim().toLowerCase());
    return matchesStatus && matchesSearch;
  });

  return (
    <main className="mx-auto flex min-h-full w-full max-w-4xl flex-1 flex-col gap-6 p-6">
      <AdminNav />

      <div>
        <h1 className="text-xl font-semibold tracking-tight">Assinaturas</h1>
        <p className="text-muted-foreground text-sm">Status de cobranca de todos os estabelecimentos.</p>
      </div>

      <div className="flex flex-wrap gap-3">
        <Input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Buscar por estabelecimento..."
          className="max-w-xs"
        />
        <Select value={statusFilter} onValueChange={(value) => setStatusFilter(value as StatusFilter)}>
          <SelectTrigger className="w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Todos os status</SelectItem>
            {STATUS_FILTERS.filter((status) => status !== "all").map((status) => (
              <SelectItem key={status} value={status}>
                {STATUS_LABELS[status]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

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
              <TableHead className="text-right">Acoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filteredSubscriptions.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="text-muted-foreground text-center">
                  Nenhuma assinatura encontrada.
                </TableCell>
              </TableRow>
            ) : (
              filteredSubscriptions.map((subscription) => (
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
                  <TableCell className="text-right">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={subscription.status === "Canceled" || cancelMutation.isPending}
                      onClick={() => setCancelingSubscription(subscription)}
                    >
                      Cancelar assinatura
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      )}

      <AlertDialog open={cancelingSubscription !== null} onOpenChange={(open) => !open && setCancelingSubscription(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancelar assinatura?</AlertDialogTitle>
            <AlertDialogDescription>
              {cancelingSubscription &&
                `Cancelar a assinatura de "${cancelingSubscription.tenantName}". Isso cancela a cobranca recorrente na Asaas de verdade. Essa acao nao pode ser desfeita.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Voltar</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={cancelMutation.isPending}
              onClick={() => {
                if (cancelingSubscription) {
                  cancelMutation.mutate(cancelingSubscription.tenantId);
                }
              }}
            >
              Cancelar assinatura
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </main>
  );
}
