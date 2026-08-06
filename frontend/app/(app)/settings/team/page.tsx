"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowLeft, Copy } from "lucide-react";

import {
  listTeamMembers,
  listPendingInvitations,
  inviteTeamMember,
  ApiError,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
  const router = useRouter();
  const { session } = useSession();
  const queryClient = useQueryClient();
  const [inviteLink, setInviteLink] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

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

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-lg flex-1 flex-col p-6 sm:p-10">
      <Link href="/" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <h1 className="text-xl font-semibold tracking-tight">Equipe</h1>
      <p className="text-muted-foreground mt-1 mb-6 text-sm">
        Membros com acesso ao painel do seu estabelecimento.
      </p>

      <section className="mb-8">
        <h2 className="mb-2 text-sm font-medium">Membros</h2>
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
      </section>

      {isOwner && (
        <>
          {invitationsQuery.data && invitationsQuery.data.length > 0 && (
            <section className="mb-8">
              <h2 className="mb-2 text-sm font-medium">Convites pendentes</h2>
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
            </section>
          )}

          <section>
            <h2 className="mb-2 text-sm font-medium">Convidar alguem</h2>
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
          </section>
        </>
      )}
    </main>
  );
}
