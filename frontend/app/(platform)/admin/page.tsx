"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { listTenantsForPlatform, setTenantActiveStatusForPlatform, ApiError } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";

export default function PlatformAdminPage() {
  const router = useRouter();
  const { session, logout } = usePlatformSession();
  const queryClient = useQueryClient();

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
      <header className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Estabelecimentos</h1>
          <p className="text-muted-foreground text-sm">Logado como {session.fullName}.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href="/admin/subscriptions">Assinaturas</Link>
          </Button>
          <Button
            variant="outline"
            onClick={() => {
              logout();
              router.replace("/admin/login");
            }}
          >
            Sair
          </Button>
        </div>
      </header>

      {tenantsQuery.isLoading ? (
        <p className="text-muted-foreground text-sm">Carregando...</p>
      ) : tenantsQuery.isError ? (
        <p className="text-destructive text-sm">Nao foi possivel carregar os estabelecimentos.</p>
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
            {tenantsQuery.data?.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground text-center">
                  Nenhum estabelecimento cadastrado.
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
                        toggleStatusMutation.mutate({ id: tenant.id, isActive: !tenant.isActive })
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
    </main>
  );
}
