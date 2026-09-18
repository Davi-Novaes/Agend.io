"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Image from "next/image";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import QRCode from "qrcode";

import {
  getPlatformMfaStatus,
  setupPlatformMfa,
  enablePlatformMfa,
  disablePlatformMfa,
  ApiError,
  type SetupMfaResult,
} from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { AdminNav } from "@/components/platform/admin-nav";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const confirmSchema = z.object({
  code: z.string().trim().min(1, "Informe o codigo."),
});
type ConfirmFormValues = z.infer<typeof confirmSchema>;

const disableSchema = z.object({
  password: z.string().min(1, "Informe a senha."),
  code: z.string().trim().min(1, "Informe o codigo."),
});
type DisableFormValues = z.infer<typeof disableSchema>;

// So "idle"/"setup"/"disable" — sem passo de codigos de recuperacao (o Super
// Admin nao tem, ver PlatformAdmin.DisableMfa no backend).
type Step = "idle" | "setup" | "disable";

export default function PlatformSecuritySettingsPage() {
  const router = useRouter();
  const { session } = usePlatformSession();
  const queryClient = useQueryClient();

  const [step, setStep] = React.useState<Step>("idle");
  const [pendingSetup, setPendingSetup] = React.useState<SetupMfaResult | null>(null);
  const [qrCodeDataUrl, setQrCodeDataUrl] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  React.useEffect(() => {
    if (!session) {
      router.replace("/admin/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const statusQuery = useQuery({
    queryKey: ["platform-mfa-status"],
    queryFn: () => getPlatformMfaStatus(accessToken),
    enabled: Boolean(session),
  });

  const confirmForm = useForm<ConfirmFormValues>({
    resolver: zodResolver(confirmSchema),
    defaultValues: { code: "" },
  });

  const disableForm = useForm<DisableFormValues>({
    resolver: zodResolver(disableSchema),
    defaultValues: { password: "", code: "" },
  });

  async function startSetup() {
    try {
      const setup = await setupPlatformMfa(accessToken);
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
      await enablePlatformMfa({ secret: pendingSetup.secret, code: values.code.trim() }, accessToken);
      toast.success("MFA habilitado.");
      setStep("idle");
      queryClient.invalidateQueries({ queryKey: ["platform-mfa-status"] });
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Codigo invalido.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function onDisable(values: DisableFormValues) {
    setIsSubmitting(true);
    try {
      await disablePlatformMfa(values, accessToken);
      toast.success("MFA desabilitado.");
      disableForm.reset({ password: "", code: "" });
      setStep("idle");
      queryClient.invalidateQueries({ queryKey: ["platform-mfa-status"] });
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel desabilitar o MFA.");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto w-full max-w-4xl">
      <AdminNav />

      <div className="mx-auto flex w-full max-w-lg flex-1 flex-col">
        <p className="text-muted-foreground mb-6 text-sm">
          Verificação em duas etapas (MFA) para a conta de Super Admin — recomendado, já que esta conta tem acesso
          a todos os estabelecimentos da plataforma.
        </p>

        {step === "idle" && (
          <Card>
            <CardContent className="p-4">
              {statusQuery.isLoading ? (
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
                    MFA está desabilitado. Habilite para exigir um código do seu aplicativo autenticador a cada
                    login, além da senha.
                  </p>
                  <Button className="mt-4" onClick={startSetup}>
                    Habilitar MFA
                  </Button>
                </>
              )}
            </CardContent>
          </Card>
        )}

        {step === "setup" && pendingSetup && (
          <Card>
            <CardContent className="p-4">
              <h2 className="mb-2 text-sm font-medium">1. Escaneie o QR code</h2>
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
              <code className="bg-muted mt-1 block rounded-md p-2 text-sm break-all select-all">
                {pendingSetup.secret}
              </code>

              <h2 className="mt-6 mb-2 text-sm font-medium">2. Confirme com um código</h2>
              <Form {...confirmForm}>
                <form onSubmit={confirmForm.handleSubmit(onConfirmSetup)} className="flex flex-col gap-3">
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
            </CardContent>
          </Card>
        )}

        {step === "disable" && (
          <Card>
            <CardContent className="p-4">
              <h2 className="mb-2 text-sm font-medium">Desabilitar MFA</h2>
              <p className="text-muted-foreground mb-3 text-sm">Confirme sua senha e o código do aplicativo autenticador.</p>
              <Form {...disableForm}>
                <form onSubmit={disableForm.handleSubmit(onDisable)} className="flex flex-col gap-3">
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
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
