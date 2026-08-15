"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Bell } from "lucide-react";

import {
  getTenantProfile,
  updateTenantReminderSettings,
  updateTenantNoShowPolicy,
  listNotificationHistory,
  ApiError,
  type TenantProfile,
  type NotificationLogItem,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { EmptyState } from "@/components/ui/empty-state";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const PAGE_SIZE = 20;

const settingsSchema = z.object({
  reminder24hEnabled: z.boolean(),
  reminder2hEnabled: z.boolean(),
  postServiceThankYouEnabled: z.boolean(),
});
type SettingsFormValues = z.infer<typeof settingsSchema>;

const noShowPolicySchema = z.object({
  requireDepositAfterNoShows: z.boolean(),
  noShowThresholdForDeposit: z.coerce.number().int().min(1, "Precisa ser pelo menos 1 falta."),
});
// z.coerce faz o tipo de entrada (antes da validacao) divergir do de saida
// (depois da coercao) — mesmo padrao de settings/loyalty/page.tsx.
type NoShowPolicyFormValues = z.output<typeof noShowPolicySchema>;
type NoShowPolicyFormInput = z.input<typeof noShowPolicySchema>;

const CHANNEL_LABEL: Record<NotificationLogItem["channel"], string> = {
  Email: "E-mail",
  WhatsApp: "WhatsApp",
};

const TRIGGER_LABEL: Record<NotificationLogItem["trigger"], string> = {
  Scheduled: "Agendamento confirmado",
  Reminder: "Lembrete",
  Cancelled: "Cancelamento",
  Rescheduled: "Reagendamento",
  Confirmed: "Confirmação de presença",
  Completed: "Pós-atendimento",
};

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString("pt-BR");
}

export default function NotificationsSettingsPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";

  const profileQuery = useQuery({
    queryKey: ["tenant", "profile"],
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6">
      <p className="text-muted-foreground text-sm">
        Controle os lembretes automáticos enviados aos clientes e veja o histórico de mensagens já enviadas.
      </p>

      {profileQuery.isLoading || !profileQuery.data ? (
        <Skeleton className="h-64 w-full" />
      ) : (
        <ReminderSettingsCard profile={profileQuery.data} accessToken={accessToken} />
      )}

      {profileQuery.isLoading || !profileQuery.data ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <NoShowPolicyCard profile={profileQuery.data} accessToken={accessToken} />
      )}

      <NotificationHistoryCard accessToken={accessToken} enabled={Boolean(session)} />
    </div>
  );
}

function ReminderSettingsCard({ profile, accessToken }: { profile: TenantProfile; accessToken: string }) {
  const queryClient = useQueryClient();

  const form = useForm<SettingsFormValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: {
      reminder24hEnabled: profile.reminder24hEnabled,
      reminder2hEnabled: profile.reminder2hEnabled,
      postServiceThankYouEnabled: profile.postServiceThankYouEnabled,
    },
  });

  const mutation = useMutation({
    mutationFn: (values: SettingsFormValues) => updateTenantReminderSettings(values, accessToken),
    onSuccess: () => {
      toast.success("Configurações de lembrete atualizadas.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar as configurações."),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Lembretes automáticos</CardTitle>
        <CardDescription>
          Ligados por padrão — funcionam por e-mail e, se o WhatsApp estiver conectado, também por WhatsApp.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
            <FormField
              control={form.control}
              name="reminder24hEnabled"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between gap-4">
                  <FormLabel>Lembrete 24 horas antes do agendamento</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} aria-label="Lembrete 24 horas antes" />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="reminder2hEnabled"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between gap-4">
                  <FormLabel>Lembrete 2 horas antes do agendamento</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} aria-label="Lembrete 2 horas antes" />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="postServiceThankYouEnabled"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between gap-4">
                  <FormLabel>Agradecimento após o atendimento</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} aria-label="Agradecimento pós-atendimento" />
                  </FormControl>
                </FormItem>
              )}
            />

            <Button type="submit" disabled={mutation.isPending} className="mt-2 w-fit">
              {mutation.isPending ? "Salvando..." : "Salvar lembretes"}
            </Button>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

function NoShowPolicyCard({ profile, accessToken }: { profile: TenantProfile; accessToken: string }) {
  const queryClient = useQueryClient();

  const form = useForm<NoShowPolicyFormInput, unknown, NoShowPolicyFormValues>({
    resolver: zodResolver(noShowPolicySchema),
    defaultValues: {
      requireDepositAfterNoShows: profile.requireDepositAfterNoShows,
      noShowThresholdForDeposit: profile.noShowThresholdForDeposit,
    },
  });

  const mutation = useMutation({
    mutationFn: (values: NoShowPolicyFormValues) => updateTenantNoShowPolicy(values, accessToken),
    onSuccess: () => {
      toast.success("Política de faltas atualizada.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar a política de faltas."),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Política de faltas</CardTitle>
        <CardDescription>
          Desligado por padrão. Se ativado, o perfil do cliente mostra um aviso sugerindo exigir sinal após o número de
          faltas configurado — nunca bloqueia o agendamento automaticamente.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
            <FormField
              control={form.control}
              name="requireDepositAfterNoShows"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between gap-4">
                  <FormLabel>Avisar sobre exigir sinal após faltas recorrentes</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} aria-label="Avisar sobre exigir sinal" />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="noShowThresholdForDeposit"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Número de faltas para exibir o aviso</FormLabel>
                  <FormControl>
                    <Input type="number" min={1} {...field} value={field.value as number} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <Button type="submit" disabled={mutation.isPending} className="mt-2 w-fit">
              {mutation.isPending ? "Salvando..." : "Salvar política de faltas"}
            </Button>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

function NotificationHistoryCard({ accessToken, enabled }: { accessToken: string; enabled: boolean }) {
  const [page, setPage] = React.useState(1);

  const historyQuery = useQuery({
    queryKey: ["notifications", "history", { page }],
    queryFn: () => listNotificationHistory({ page, pageSize: PAGE_SIZE }, accessToken),
    enabled,
    placeholderData: (previous) => previous,
  });

  const totalPages = Math.max(1, Math.ceil((historyQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Histórico de mensagens</CardTitle>
        <CardDescription>E-mails e mensagens de WhatsApp enviados aos clientes, mais recentes primeiro.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Data</TableHead>
              <TableHead>Cliente</TableHead>
              <TableHead>Serviço</TableHead>
              <TableHead>Canal</TableHead>
              <TableHead>Tipo</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {historyQuery.isLoading ? (
              Array.from({ length: 5 }).map((_, index) => (
                <TableRow key={index}>
                  <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-16" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-16" /></TableCell>
                </TableRow>
              ))
            ) : historyQuery.data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="p-0">
                  <EmptyState
                    icon={Bell}
                    title="Nenhuma mensagem enviada ainda"
                    description="Confirmações, lembretes e outras notificações automáticas aparecem aqui assim que forem enviadas."
                  />
                </TableCell>
              </TableRow>
            ) : (
              historyQuery.data?.items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="whitespace-nowrap">{formatDateTime(item.sentAtUtc)}</TableCell>
                  <TableCell>{item.customerName}</TableCell>
                  <TableCell>{item.serviceName}</TableCell>
                  <TableCell>{CHANNEL_LABEL[item.channel]}</TableCell>
                  <TableCell>{TRIGGER_LABEL[item.trigger]}</TableCell>
                  <TableCell>
                    <Badge variant={item.status === "Sent" ? "success" : "destructive"} title={item.errorMessage ?? undefined}>
                      {item.status === "Sent" ? "Enviado" : "Falhou"}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>

        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground" aria-live="polite">
            Página {historyQuery.data?.page ?? page} de {totalPages}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
              Anterior
            </Button>
            <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
              Próxima
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
