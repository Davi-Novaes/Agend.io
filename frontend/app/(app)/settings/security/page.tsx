"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import QRCode from "qrcode";
import { ArrowLeft, Copy } from "lucide-react";

import { getMfaStatus, setupMfa, enableMfa, disableMfa, ApiError, type SetupMfaResult } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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

// Passo em que a tela esta: "idle" mostra o estado atual (habilitado/desabilitado);
// os demais so existem durante o fluxo de habilitar/desabilitar.
type Step = "idle" | "setup" | "recovery-codes" | "disable";

export default function SecuritySettingsPage() {
  const router = useRouter();
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [step, setStep] = React.useState<Step>("idle");
  const [pendingSetup, setPendingSetup] = React.useState<SetupMfaResult | null>(null);
  const [qrCodeDataUrl, setQrCodeDataUrl] = React.useState<string | null>(null);
  const [recoveryCodes, setRecoveryCodes] = React.useState<string[]>([]);
  const [recoveryCodesAcknowledged, setRecoveryCodesAcknowledged] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const statusQuery = useQuery({
    queryKey: ["mfa-status"],
    queryFn: () => getMfaStatus(accessToken),
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

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-lg flex-1 flex-col p-6 sm:p-10">
      <Link href="/painel" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <h1 className="text-xl font-semibold tracking-tight">Segurança</h1>
      <p className="text-muted-foreground mt-1 mb-6 text-sm">
        Verificação em duas etapas (MFA) para a sua conta.
      </p>

      {step === "idle" && (
        <section className="rounded-lg border p-4">
          {statusQuery.isLoading ? (
            <p className="text-muted-foreground text-sm">Carregando...</p>
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
          )}
        </section>
      )}

      {step === "setup" && pendingSetup && (
        <section className="rounded-lg border p-4">
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
          <code className="bg-muted mt-1 block rounded-md p-2 text-sm break-all select-all">{pendingSetup.secret}</code>

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
        </section>
      )}

      {step === "recovery-codes" && (
        <section className="rounded-lg border p-4">
          <h2 className="mb-2 text-sm font-medium">MFA habilitado — guarde seus códigos de recuperação</h2>
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
        </section>
      )}

      {step === "disable" && (
        <section className="rounded-lg border p-4">
          <h2 className="mb-2 text-sm font-medium">Desabilitar MFA</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            Confirme sua senha e um código (do aplicativo autenticador ou de recuperação).
          </p>
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
        </section>
      )}
    </main>
  );
}
