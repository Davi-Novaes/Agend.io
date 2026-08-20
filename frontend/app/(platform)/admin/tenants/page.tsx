"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { AlertTriangle, Building2 } from "lucide-react";

import { listTenantsForPlatform, setTenantActiveStatusForPlatform, ApiError, type TenantAdminSummary } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { AdminNav } from "@/components/platform/admin-nav";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
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

export default function PlatformTenantsPage() {
  const router = useRouter();
  const { session } = usePlatformSession();
  const queryClient = useQueryClient();
  // Desativar bloqueia o acesso de um cliente pagante ao sistema inteiro —
  // precisa do mesmo AlertDialog de confirmacao ja usado em admin/subscriptions
  // para acao de risco equivalente (BL-07, docs/BACKLOG.md). Ativar nao e
  // destrutivo, entao continua direto no clique.
  const [deactivating, setDeactivating] = React.useState<TenantAdminSummary | null>(null);

  React.useEffect(() => {
    if (!session) {
      router.replace("/admin/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const tenantsQuery = useQuery({
    queryKey: ["platform", "tenants"],
    queryFn: () => listTenantsForPlatform(accessToken),
    enabled: Boolean(session),
  });

  const toggleStatusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      setTenantActiveStatusForPlatform(id, isActive, accessToken),
    onSuccess: () => {
      setDeactivating(null);
      queryClient.invalidateQueries({ queryKey: ["platform", "tenants"] });
    },
    onError: (error: unknown) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o estabelecimento.");
    },
  });

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-4xl flex-1 flex-col gap-6 p-6">
      <AdminNav />

      <div>
        <h1 className="text-xl font-semibold tracking-tight">Estabelecimentos</h1>
        <p className="text-muted-foreground text-sm">Todos os estabelecimentos cadastrados na plataforma.</p>
      </div>

      {tenantsQuery.isError ? (
        <EmptyState
          icon={AlertTriangle}
          title="Nao foi possivel carregar os estabelecimentos"
          description="Tente recarregar a pagina em instantes."
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Nome</TableHead>
              <TableHead>Identificador</TableHead>
              <TableHead>Fuso horario</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Acoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {tenantsQuery.isLoading ? (
              Array.from({ length: 5 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-28" /></TableCell>
                  <TableCell><Skeleton className="h-5 w-14 rounded-full" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-8 w-20" /></TableCell>
                </TableRow>
              ))
            ) : tenantsQuery.data?.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="p-0">
                  <EmptyState icon={Building2} title="Nenhum estabelecimento cadastrado" />
                </TableCell>
              </TableRow>
            ) : (
              tenantsQuery.data?.map((tenant) => (
                <TableRow key={tenant.id}>
                  <TableCell className="font-medium">{tenant.name}</TableCell>
                  <TableCell>{tenant.slug}</TableCell>
                  <TableCell>{tenant.timeZoneId}</TableCell>
                  <TableCell>
                    <Badge variant={tenant.isActive ? "default" : "secondary"}>
                      {tenant.isActive ? "Ativo" : "Inativo"}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={toggleStatusMutation.isPending}
                      onClick={() =>
                        tenant.isActive
                          ? setDeactivating(tenant)
                          : toggleStatusMutation.mutate({ id: tenant.id, isActive: true })
                      }
                    >
                      {tenant.isActive ? "Desativar" : "Ativar"}
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      )}

      <AlertDialog open={deactivating !== null} onOpenChange={(open) => !open && setDeactivating(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Desativar estabelecimento?</AlertDialogTitle>
            <AlertDialogDescription>
              {deactivating &&
                `Isso bloqueia imediatamente o acesso de "${deactivating.name}" (todo o time e clientes) ao sistema. Voce pode reativar depois clicando em "Ativar".`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Voltar</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={toggleStatusMutation.isPending}
              onClick={() => {
                if (deactivating) {
                  toggleStatusMutation.mutate({ id: deactivating.id, isActive: false });
                }
              }}
            >
              {toggleStatusMutation.isPending ? "Desativando..." : "Desativar"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </main>
  );
}
