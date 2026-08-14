"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import {
  getTenantProfile,
  updateTenantWhatsAppSettings,
  ApiError,
  type TenantProfile,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const settingsSchema = z.object({
  enabled: z.boolean(),
  phoneNumberId: z.string(),
  accessToken: z.string(),
  scheduledTemplate: z.string(),
  reminderTemplate: z.string(),
  cancelledTemplate: z.string(),
  rescheduledTemplate: z.string(),
  confirmedTemplate: z.string(),
  completedTemplate: z.string(),
});

type SettingsFormValues = z.infer<typeof settingsSchema>;
type TemplateFieldName = Exclude<keyof SettingsFormValues, "enabled">;

const TEMPLATE_FIELDS: { name: TemplateFieldName; label: string; placeholder: string }[] = [
  {
    name: "scheduledTemplate",
    label: "Agendamento confirmado",
    placeholder: "Ola {{cliente}}! Seu agendamento de {{servico}} em {{estabelecimento}} foi confirmado para {{data}} as {{hora}}.",
  },
  {
    name: "reminderTemplate",
    label: "Lembrete",
    placeholder: "Lembrete: seu agendamento de {{servico}} em {{estabelecimento}} e {{data}} as {{hora}}.",
  },
  {
    name: "cancelledTemplate",
    label: "Cancelamento",
    placeholder: "Seu agendamento de {{servico}} em {{estabelecimento}} marcado para {{data}} as {{hora}} foi cancelado.",
  },
  {
    name: "rescheduledTemplate",
    label: "Reagendamento",
    placeholder: "Seu agendamento de {{servico}} em {{estabelecimento}} foi remarcado para {{data}} as {{hora}}.",
  },
  {
    name: "confirmedTemplate",
    label: "Confirmação de presença",
    placeholder: "Seu agendamento de {{servico}} em {{estabelecimento}} para {{data}} as {{hora}} esta confirmado. Contamos com voce!",
  },
  {
    name: "completedTemplate",
    label: "Pós-atendimento",
    placeholder: "Obrigado por visitar {{estabelecimento}}, {{cliente}}! Esperamos que tenha gostado do seu {{servico}}.",
  },
];

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

export default function WhatsAppSettingsPage() {
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
    <div className="mx-auto flex w-full max-w-lg flex-1 flex-col gap-6">
      <p className="text-muted-foreground text-sm">
        Conecte o WhatsApp do seu estabelecimento para enviar confirmações, lembretes e outras notificações automáticas aos clientes.
      </p>

      {profileQuery.isLoading || !profileQuery.data ? (
        <div className="flex flex-col gap-6">
          <Skeleton className="h-56 w-full" />
          <Skeleton className="h-96 w-full" />
        </div>
      ) : (
        <WhatsAppSettingsForm profile={profileQuery.data} accessToken={accessToken} />
      )}
    </div>
  );
}

function WhatsAppSettingsForm({ profile, accessToken }: { profile: TenantProfile; accessToken: string }) {
  const queryClient = useQueryClient();

  const form = useForm<SettingsFormValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: {
      enabled: profile.whatsAppIntegrationEnabled,
      phoneNumberId: profile.whatsAppPhoneNumberId ?? "",
      accessToken: "",
      scheduledTemplate: profile.whatsAppScheduledTemplate ?? "",
      reminderTemplate: profile.whatsAppReminderTemplate ?? "",
      cancelledTemplate: profile.whatsAppCancelledTemplate ?? "",
      rescheduledTemplate: profile.whatsAppRescheduledTemplate ?? "",
      confirmedTemplate: profile.whatsAppConfirmedTemplate ?? "",
      completedTemplate: profile.whatsAppCompletedTemplate ?? "",
    },
  });

  const enabled = form.watch("enabled");
  const accessTokenValue = form.watch("accessToken");

  const mutation = useMutation({
    mutationFn: (values: SettingsFormValues) =>
      updateTenantWhatsAppSettings(
        {
          enabled: values.enabled,
          phoneNumberId: toNullable(values.phoneNumberId),
          accessToken: toNullable(values.accessToken),
          scheduledTemplate: toNullable(values.scheduledTemplate),
          reminderTemplate: toNullable(values.reminderTemplate),
          cancelledTemplate: toNullable(values.cancelledTemplate),
          rescheduledTemplate: toNullable(values.rescheduledTemplate),
          confirmedTemplate: toNullable(values.confirmedTemplate),
          completedTemplate: toNullable(values.completedTemplate),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Configurações do WhatsApp atualizadas.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
      form.setValue("accessToken", "");
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar as configurações do WhatsApp."),
  });

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Conexão</CardTitle>
            <CardDescription>
              Credenciais da WhatsApp Cloud API (Meta). Sem elas, nenhuma mensagem é enviada — o restante do produto continua funcionando normalmente.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            <FormField
              control={form.control}
              name="enabled"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between gap-4">
                  <FormLabel>Ativar integração com WhatsApp</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} aria-label="Ativar integração com WhatsApp" />
                  </FormControl>
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="phoneNumberId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Phone Number ID</FormLabel>
                  <FormControl>
                    <Input placeholder="123456789012345" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="accessToken"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Token de acesso</FormLabel>
                  <FormControl>
                    <Input
                      type="password"
                      placeholder={profile.whatsAppAccessTokenConfigured ? "•••••••••••••• (já configurado)" : "Cole o token de acesso"}
                      {...field}
                    />
                  </FormControl>
                  <p className="text-muted-foreground text-xs">
                    {profile.whatsAppAccessTokenConfigured
                      ? "Um token já está salvo. Deixe em branco para mantê-lo, ou digite um novo para substituí-lo."
                      : "Necessário para ativar a integração."}
                  </p>
                  <FormMessage />
                </FormItem>
              )}
            />

            {enabled && !profile.whatsAppAccessTokenConfigured && accessTokenValue.trim() === "" && (
              <p className="text-destructive text-sm" role="status">
                Informe o Phone Number ID e o token de acesso para ativar a integração.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Templates de mensagem</CardTitle>
            <CardDescription>
              Deixe em branco para usar o texto padrão. Placeholders disponíveis:{" "}
              <code className="text-xs">{"{{cliente}}"}</code>, <code className="text-xs">{"{{servico}}"}</code>,{" "}
              <code className="text-xs">{"{{data}}"}</code>, <code className="text-xs">{"{{hora}}"}</code> e{" "}
              <code className="text-xs">{"{{estabelecimento}}"}</code>.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            {TEMPLATE_FIELDS.map((templateField) => (
              <FormField
                key={templateField.name}
                control={form.control}
                name={templateField.name}
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{templateField.label}</FormLabel>
                    <FormControl>
                      <Textarea rows={2} placeholder={templateField.placeholder} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            ))}
          </CardContent>
        </Card>

        <Button type="submit" disabled={mutation.isPending} className="w-fit">
          {mutation.isPending ? "Salvando..." : "Salvar configurações do WhatsApp"}
        </Button>
      </form>
    </Form>
  );
}
