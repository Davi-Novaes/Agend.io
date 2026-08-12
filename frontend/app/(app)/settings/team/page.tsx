"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Copy } from "lucide-react";

import {
  listTeamMembers,
  listPendingInvitations,
  inviteTeamMember,
  ApiError,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";

const inviteSchema = z.object({
  email: z.email("Informe um e-mail valido."),
  role: z.enum(["Staff", "Owner"]),
});

type InviteFormValues = z.infer<typeof inviteSchema>;

export default function TeamSettingsPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();
  const [inviteLink, setInviteLink] = React.useState<string | null>(null);

  const accessToken = session?.accessToken ?? "";

  const membersQuery = useQuery({
    queryKey: ["team-members"],
    queryFn: () => listTeamMembers(accessToken),
    enabled: Boolean(session),
  });

  // 403 aqui so significa "quem esta logado nao e Owner" — usamos a propria
  // decisao do backend para decidir se mostramos a secao de convites, em vez
  // de duplicar a checagem de papel no frontend.
  const invitationsQuery = useQuery({
    queryKey: ["pending-invitations"],
    queryFn: () => listPendingInvitations(accessToken),
    enabled: Boolean(session),
    retry: false,
  });

  const isOwner = invitationsQuery.isSuccess;

  const inviteMutation = useMutation({
    mutationFn: (values: InviteFormValues) => inviteTeamMember(values, accessToken),
    onSuccess: (result) => {
      setInviteLink(`${window.location.origin}/invitations/${result.token}`);
      queryClient.invalidateQueries({ queryKey: ["pending-invitations"] });
      form.reset();
    },
    onError: (error) => {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel enviar o convite.";
      toast.error(message);
    },
  });

  const form = useForm<InviteFormValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: "", role: "Staff" },
  });

  return (
    <div className="mx-auto flex w-full max-w-lg flex-1 flex-col gap-6">
      <p className="text-muted-foreground text-sm">
        Membros com acesso ao painel do seu estabelecimento.
      </p>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Membros</CardTitle>
        </CardHeader>
        <CardContent>
          {membersQuery.isLoading ? (
            <div className="flex flex-col gap-3">
              {Array.from({ length: 2 }).map((_, index) => (
                <Skeleton key={index} className="h-12 w-full rounded-lg" />
              ))}
            </div>
          ) : (
            <ul className="divide-border divide-y rounded-lg border">
              {membersQuery.data?.map((member) => (
                <li key={member.id} className="flex items-center justify-between gap-3 p-3 text-sm">
                  <div>
                    <p className="font-medium">{member.fullName}</p>
                    <p className="text-muted-foreground">{member.email}</p>
                  </div>
                  <span className="bg-muted rounded-full px-2 py-0.5 text-xs">
                    {member.role === "Owner" ? "Dono" : "Equipe"}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {isOwner && (
        <>
          {invitationsQuery.data && invitationsQuery.data.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Convites pendentes</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="divide-border divide-y rounded-lg border">
                  {invitationsQuery.data.map((invitation) => (
                    <li key={invitation.id} className="flex items-center justify-between gap-3 p-3 text-sm">
                      <span>{invitation.email}</span>
                      <span className="text-muted-foreground text-xs">
                        expira em {new Date(invitation.expiresAtUtc).toLocaleDateString("pt-BR")}
                      </span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Convidar alguem</CardTitle>
            </CardHeader>
            <CardContent>
            <Form {...form}>
              <form
                onSubmit={form.handleSubmit((values) => inviteMutation.mutate(values))}
                className="flex flex-col gap-3 sm:flex-row sm:items-end"
              >
                <FormField
                  control={form.control}
                  name="email"
                  render={({ field }) => (
                    <FormItem className="flex-1">
                      <FormLabel>E-mail</FormLabel>
                      <FormControl>
                        <Input type="email" placeholder="pessoa@exemplo.com" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="role"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Papel</FormLabel>
                      <FormControl>
                        <select
                          {...field}
                          className="border-input bg-background flex h-9 rounded-md border px-3 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
                        >
                          <option value="Staff">Equipe</option>
                          <option value="Owner">Dono</option>
                        </select>
                      </FormControl>
                    </FormItem>
                  )}
                />
                <Button type="submit" disabled={inviteMutation.isPending}>
                  {inviteMutation.isPending ? "Enviando..." : "Convidar"}
                </Button>
              </form>
            </Form>

            {inviteLink && (
              <div className="bg-muted mt-4 flex items-center justify-between gap-2 rounded-lg p-3 text-sm">
                <span className="truncate">{inviteLink}</span>
                <button
                  type="button"
                  onClick={() => {
                    navigator.clipboard.writeText(inviteLink);
                    toast.success("Link copiado.");
                  }}
                  className="text-muted-foreground hover:text-foreground shrink-0"
                  aria-label="Copiar link do convite"
                >
                  <Copy className="size-4" />
                </button>
              </div>
            )}
            <p className="text-muted-foreground mt-2 text-xs">
              Envio automatico por e-mail chega em uma proxima etapa — por enquanto, compartilhe o link acima.
            </p>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
