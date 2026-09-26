"use client";

import * as React from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import QRCode from "qrcode";
import { CalendarClock, Copy, Globe2, History, MonitorSmartphone, Network } from "lucide-react";

import {
  getMfaStatus,
  getMySecurityActivityLog,
  setupMfa,
  enableMfa,
  disableMfa,
  changePassword,
  logoutAllSessions,
  ApiError,
  type SetupMfaResult,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { strongPasswordSchema } from "@/lib/validation/password";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/ui/empty-state";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "Informe a senha atual."),
    newPassword: strongPasswordSchema,
    confirmPassword: z.string().min(1, "Confirme a nova senha."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "As senhas não coincidem.",
    path: ["confirmPassword"],
  });
type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;

const confirmSchema = z.object({
  code: z.string().trim().min(1, "Informe o codigo."),
});
type ConfirmFormValues = z.infer<typeof confirmSchema>;

const disableSchema = z.object({
  password: z.string().min(1, "Informe a senha."),
  code: z.string().trim().min(1, "Informe o codigo."),
});
type DisableFormValues = z.infer<typeof disableSchema>;

// Passo em que a tela esta: "idle" mostra o estado atual (habilitado/desabilitado);
// os demais so existem durante o fluxo de habilitar/desabilitar.
type Step = "idle" | "setup" | "recovery-codes" | "disable";

function formatOccurredAt(occurredAtUtc: string): string {
  return new Date(occurredAtUtc).toLocaleString("pt-BR", { dateStyle: "medium", timeStyle: "short" });
}

function locationFrom(entry: { city: string | null; region: string | null; countryCode: string | null }): string | null {
  let country = entry.countryCode;
  if (country && typeof Intl.DisplayNames === "function") {
    country = new Intl.DisplayNames(["pt-BR"], { type: "region" }).of(country.toUpperCase()) ?? country;
  }

  const parts = [entry.city, entry.region, country].filter(Boolean);
  return parts.length > 0 ? parts.join(", ") : null;
}

function normalizeIpAddress(ipAddress: string | null): string | null {
  if (!ipAddress) return null;
  return ipAddress.replace(/^::ffff:/i, "");
}

function deviceFrom(userAgent: string | null): string | null {
  if (!userAgent) return null;

  const browser = userAgent.includes("Edg/")
    ? "Microsoft Edge"
    : userAgent.includes("Firefox/")
      ? "Firefox"
      : userAgent.includes("Chrome/")
        ? "Google Chrome"
        : userAgent.includes("Safari/")
          ? "Safari"
          : "Navegador não identificado";

  const system = /Android/i.test(userAgent)
    ? "Android"
    : /iPhone|iPad/i.test(userAgent)
      ? "iPhone/iPad"
      : /Windows/i.test(userAgent)
        ? "Windows"
        : /Mac OS X/i.test(userAgent)
          ? "macOS"
          : /Linux/i.test(userAgent)
            ? "Linux"
            : null;

  return system ? `${browser} em ${system}` : browser;
}

const SECURITY_EVENT_LABELS: Record<string, string> = {
  LoginSucceeded: "Login realizado",
  LoginFailed: "Tentativa de login sem sucesso",
  PasswordResetRequested: "Redefinição de senha solicitada",
  PasswordResetCompleted: "Senha redefinida",
  EmailConfirmed: "E-mail confirmado",
  RegistrationConfirmed: "Cadastro confirmado",
  MfaEnabled: "Verificação em duas etapas ativada",
  MfaDisabled: "Verificação em duas etapas desativada",
};

function securityEventLabel(eventType: string): string {
  return SECURITY_EVENT_LABELS[eventType] ?? "Atividade de segurança";
}

export default function SecuritySettingsPage() {
  const router = useRouter();
  const { session, logout } = useSession();
  const queryClient = useQueryClient();

  const [step, setStep] = React.useState<Step>("idle");
  const [pendingSetup, setPendingSetup] = React.useState<SetupMfaResult | null>(null);
  const [qrCodeDataUrl, setQrCodeDataUrl] = React.useState<string | null>(null);
  const [recoveryCodes, setRecoveryCodes] = React.useState<string[]>([]);
  const [recoveryCodesAcknowledged, setRecoveryCodesAcknowledged] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [isChangingPassword, setIsChangingPassword] = React.useState(false);
  const [isLoggingOutAll, setIsLoggingOutAll] = React.useState(false);
  const [showAllActivity, setShowAllActivity] = React.useState(false);

  const accessToken = session?.accessToken ?? "";

  const statusQuery = useQuery({
    queryKey: ["mfa-status"],
    queryFn: () => getMfaStatus(accessToken),
    enabled: Boolean(session),
  });

  // Sem paginacao: e um log recente (nao um historico completo), mesmo
  // espirito de listNotificationHistory mas sem a necessidade de Prev/Next
  // aqui — o endpoint ja devolve so as entradas mais relevantes.
  const activityQuery = useQuery({
    queryKey: ["security-activity-log"],
    queryFn: () => getMySecurityActivityLog(accessToken),
    enabled: Boolean(session),
  });

  const changePasswordForm = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  const confirmForm = useForm<ConfirmFormValues>({
    resolver: zodResolver(confirmSchema),
    defaultValues: { code: "" },
  });

  const disableForm = useForm<DisableFormValues>({
    resolver: zodResolver(disableSchema),
    defaultValues: { password: "", code: "" },
  });

  async function onChangePassword(values: ChangePasswordFormValues) {
    setIsChangingPassword(true);
    try {
      await changePassword({ currentPassword: values.currentPassword, newPassword: values.newPassword }, accessToken);
      // Troca de senha revoga todas as sessoes no servidor (ver
      // ChangePasswordCommandHandler) — desloga aqui tambem pra nao deixar a
      // pessoa com a falsa impressao de que a sessao atual continua intacta.
      toast.success("Senha alterada. Entre novamente.");
      logout();
      router.replace("/login");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel trocar a senha.";
      toast.error(message);
    } finally {
      setIsChangingPassword(false);
    }
  }

  async function onLogoutAllSessions() {
    setIsLoggingOutAll(true);
    try {
      await logoutAllSessions(accessToken);
      toast.success("Todas as sessões foram encerradas. Entre novamente.");
      logout();
      router.replace("/login");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel encerrar as sessoes.";
      toast.error(message);
    } finally {
      setIsLoggingOutAll(false);
    }
  }

  async function startSetup() {
    try {
      const setup = await setupMfa(accessToken);
      setPendingSetup(setup);
      setQrCodeDataUrl(await QRCode.toDataURL(setup.otpAuthUri));
      confirmForm.reset({ code: "" });
      setStep("setup");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel iniciar a configuracao do MFA.");
    }
  }

  async function onConfirmSetup(values: ConfirmFormValues) {
    if (!pendingSetup) {
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await enableMfa({ secret: pendingSetup.secret, code: values.code.trim() }, accessToken);
      setRecoveryCodes(result.recoveryCodes);
      setRecoveryCodesAcknowledged(false);
      setStep("recovery-codes");
      queryClient.invalidateQueries({ queryKey: ["mfa-status"] });
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Codigo invalido.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function onDisable(values: DisableFormValues) {
    setIsSubmitting(true);
    try {
      await disableMfa(values, accessToken);
      toast.success("MFA desabilitado.");
      disableForm.reset({ password: "", code: "" });
      setStep("idle");
      queryClient.invalidateQueries({ queryKey: ["mfa-status"] });
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel desabilitar o MFA.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="flex w-full max-w-4xl flex-1 flex-col gap-6">
      <div>
        <p className="text-muted-foreground text-sm">Proteção da sua conta — senha, verificação em duas etapas e sessões.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Trocar senha</CardTitle>
          <CardDescription>Ao concluir, todas as sessões ativas (inclusive esta) são encerradas por segurança.</CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...changePasswordForm}>
            <form onSubmit={changePasswordForm.handleSubmit(onChangePassword)} className="flex max-w-sm flex-col gap-3">
              <FormField
                control={changePasswordForm.control}
                name="currentPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Senha atual</FormLabel>
                    <FormControl>
                      <Input type="password" autoComplete="current-password" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={changePasswordForm.control}
                name="newPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nova senha</FormLabel>
                    <FormControl>
                      <Input type="password" autoComplete="new-password" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={changePasswordForm.control}
                name="confirmPassword"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Confirme a nova senha</FormLabel>
                    <FormControl>
                      <Input type="password" autoComplete="new-password" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <Button type="submit" disabled={isChangingPassword} className="mt-1 self-start">
                {isChangingPassword ? "Salvando..." : "Trocar senha"}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Verificação em duas etapas</CardTitle>
          <CardDescription>MFA (código de aplicativo autenticador) para a sua conta.</CardDescription>
        </CardHeader>
        <CardContent>
          {step === "idle" &&
            (statusQuery.isLoading ? (
              <Skeleton className="h-16 w-full" />
            ) : statusQuery.data?.mfaEnabled ? (
              <>
                <p className="text-sm">
                  <span className="font-medium">MFA habilitado.</span> Um código do seu aplicativo autenticador é
                  exigido a cada login.
                </p>
                <Button variant="outline" className="mt-4" onClick={() => setStep("disable")}>
                  Desabilitar MFA
                </Button>
              </>
            ) : (
              <>
                <p className="text-sm">
                  MFA está desabilitado. Habilite para exigir um código do seu aplicativo autenticador a cada login,
                  além da senha.
                </p>
                <Button className="mt-4" onClick={startSetup}>
                  Habilitar MFA
                </Button>
              </>
            ))}

          {step === "setup" && pendingSetup && (
            <div>
              <h3 className="mb-2 text-sm font-medium">1. Escaneie o QR code</h3>
              <p className="text-muted-foreground mb-3 text-sm">
                Use um aplicativo autenticador (Google Authenticator, Authy, 1Password...).
              </p>
              {qrCodeDataUrl && (
                <Image
                  src={qrCodeDataUrl}
                  alt="QR code para configurar o aplicativo autenticador"
                  width={200}
                  height={200}
                  className="rounded-md border"
                  unoptimized
                />
              )}

              <p className="text-muted-foreground mt-3 text-sm">
                Não consegue escanear? Digite esta chave manualmente no aplicativo:
              </p>
              <code className="bg-muted mt-1 block rounded-md p-2 text-sm break-all select-all">{pendingSetup.secret}</code>

              <h3 className="mt-6 mb-2 text-sm font-medium">2. Confirme com um código</h3>
              <Form {...confirmForm}>
                <form onSubmit={confirmForm.handleSubmit(onConfirmSetup)} className="flex max-w-sm flex-col gap-3">
                  <FormField
                    control={confirmForm.control}
                    name="code"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Código de 6 dígitos</FormLabel>
                        <FormControl>
                          <Input inputMode="numeric" autoComplete="one-time-code" placeholder="000000" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <div className="flex gap-2">
                    <Button type="submit" disabled={isSubmitting}>
                      {isSubmitting ? "Confirmando..." : "Confirmar e habilitar"}
                    </Button>
                    <Button type="button" variant="outline" onClick={() => setStep("idle")}>
                      Cancelar
                    </Button>
                  </div>
                </form>
              </Form>
            </div>
          )}

          {step === "recovery-codes" && (
            <div>
              <h3 className="mb-2 text-sm font-medium">MFA habilitado — guarde seus códigos de recuperação</h3>
              <p className="text-muted-foreground mb-3 text-sm">
                Cada código funciona uma única vez e serve para entrar caso você perca acesso ao aplicativo
                autenticador. Esta é a única vez que eles aparecem — salve em um lugar seguro.
              </p>

              <ul className="grid grid-cols-2 gap-2 font-mono text-sm">
                {recoveryCodes.map((code) => (
                  <li key={code} className="bg-muted rounded-md p-2 text-center">
                    {code}
                  </li>
                ))}
              </ul>

              <button
                type="button"
                onClick={() => {
                  navigator.clipboard.writeText(recoveryCodes.join("\n"));
                  toast.success("Códigos copiados.");
                }}
                className="text-muted-foreground hover:text-foreground mt-3 inline-flex items-center gap-1.5 text-sm"
              >
                <Copy className="size-4" />
                Copiar todos
              </button>

              <label className="mt-4 flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={recoveryCodesAcknowledged}
                  onChange={(event) => setRecoveryCodesAcknowledged(event.target.checked)}
                  className="mt-0.5"
                />
                Eu salvei estes códigos em um lugar seguro.
              </label>

              <Button className="mt-4" disabled={!recoveryCodesAcknowledged} onClick={() => setStep("idle")}>
                Concluir
              </Button>
            </div>
          )}

          {step === "disable" && (
            <div>
              <h3 className="mb-2 text-sm font-medium">Desabilitar MFA</h3>
              <p className="text-muted-foreground mb-3 text-sm">
                Confirme sua senha e um código (do aplicativo autenticador ou de recuperação).
              </p>
              <Form {...disableForm}>
                <form onSubmit={disableForm.handleSubmit(onDisable)} className="flex max-w-sm flex-col gap-3">
                  <FormField
                    control={disableForm.control}
                    name="password"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Senha</FormLabel>
                        <FormControl>
                          <Input type="password" autoComplete="current-password" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={disableForm.control}
                    name="code"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Código</FormLabel>
                        <FormControl>
                          <Input inputMode="numeric" autoComplete="one-time-code" placeholder="000000" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <div className="flex gap-2">
                    <Button type="submit" variant="destructive" disabled={isSubmitting}>
                      {isSubmitting ? "Desabilitando..." : "Desabilitar MFA"}
                    </Button>
                    <Button type="button" variant="outline" onClick={() => setStep("idle")}>
                      Cancelar
                    </Button>
                  </div>
                </form>
              </Form>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Sessões ativas</CardTitle>
          <CardDescription>
            Se suspeitar de acesso indevido, encerre todas as sessões abertas (todos os dispositivos e abas) de uma vez —
            inclusive esta.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button variant="outline" disabled={isLoggingOutAll} onClick={onLogoutAllSessions}>
            {isLoggingOutAll ? "Encerrando..." : "Sair de todos os dispositivos"}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Atividade recente</CardTitle>
          <CardDescription>
            Confira quando e de onde sua conta foi acessada. A localização por IP é aproximada.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {activityQuery.isLoading ? (
            <div className="flex flex-col gap-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : !activityQuery.data || activityQuery.data.length === 0 ? (
            <EmptyState icon={History} title="Nenhuma atividade registrada ainda." />
          ) : (
            <div className="flex flex-col gap-3">
              <ul className="flex flex-col gap-2">
                {(showAllActivity ? activityQuery.data : activityQuery.data.slice(0, 10)).map((entry) => {
                  const location = locationFrom(entry);
                  const ipAddress = normalizeIpAddress(entry.ipAddress);
                  const device = deviceFrom(entry.userAgent);
                  return (
                    <li
                      key={entry.id}
                      className="rounded-xl border bg-card p-4 text-sm shadow-xs transition-colors hover:bg-muted/20"
                    >
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <p className="font-medium">{securityEventLabel(entry.eventType)}</p>
                          <p className="mt-0.5 text-xs text-muted-foreground">
                            Registro de segurança da sua conta
                          </p>
                        </div>
                        <Badge variant={entry.success ? "success" : "destructive"}>
                          {entry.success ? "Sucesso" : "Falhou"}
                        </Badge>
                      </div>

                      <div className="mt-4 grid gap-3 border-t pt-4 sm:grid-cols-2">
                        <div className="flex min-w-0 gap-2.5">
                          <CalendarClock className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                          <div className="min-w-0">
                            <p className="text-xs font-medium text-muted-foreground">Data e hora</p>
                            <p className="mt-0.5">{formatOccurredAt(entry.occurredAtUtc)}</p>
                          </div>
                        </div>
                        <div className="flex min-w-0 gap-2.5">
                          <Network className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                          <div className="min-w-0">
                            <p className="text-xs font-medium text-muted-foreground">Endereço IP</p>
                            <p className="mt-0.5 break-all font-mono text-xs">{ipAddress ?? "Não informado"}</p>
                          </div>
                        </div>
                        <div className="flex min-w-0 gap-2.5">
                          <Globe2 className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                          <div className="min-w-0">
                            <p className="text-xs font-medium text-muted-foreground">Localização aproximada</p>
                            <p className="mt-0.5">{location ?? "Não disponível para este acesso"}</p>
                          </div>
                        </div>
                        <div className="flex min-w-0 gap-2.5">
                          <MonitorSmartphone className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                          <div className="min-w-0">
                            <p className="text-xs font-medium text-muted-foreground">Dispositivo</p>
                            <p className="mt-0.5">{device ?? "Não identificado"}</p>
                          </div>
                        </div>
                      </div>
                    </li>
                  );
                })}
              </ul>
              {activityQuery.data.length > 10 && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="self-start"
                  onClick={() => setShowAllActivity((current) => !current)}
                >
                  {showAllActivity ? "Mostrar menos" : `Ver todos (${activityQuery.data.length})`}
                </Button>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
