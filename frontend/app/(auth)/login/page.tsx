"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Eye, EyeOff, Loader2, Lock, Mail, MailCheck, ShieldCheck, Store } from "lucide-react";

import { getTenantBySlug, resendConfirmationEmail, ApiError } from "@/lib/api/client";
import { useSession, MfaRequiredError } from "@/lib/auth/session-context";
import { Logo } from "@/components/logo";
import { LoginShowcase } from "@/components/auth/login-showcase";
import { TurnstileWidget } from "@/components/auth/turnstile-widget";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";

// Estilo dos inputs so desta tela (nao mexe no Input global, usado em toda
// a base de codigo) -- entrada mais "premium": mais alta, superficie --card
// (contrasta com o --background do painel em vez de ficar transparente e
// "apagada"), e um anel de foco maior/mais suave que o padrao do design
// system pra reforcar a cor de marca sem ficar agressivo.
const LOGIN_INPUT_BASE_CLASS =
  "h-11 rounded-xl border-border bg-card text-[15px] shadow-xs transition-all duration-200 hover:border-foreground/25 focus-visible:ring-4 focus-visible:ring-primary/15";

const loginSchema = z.object({
  tenantSlug: z
    .string()
    .trim()
    .min(1, "Informe o identificador do estabelecimento."),
  email: z.email("Informe um e-mail valido."),
  password: z.string().min(1, "Informe a senha."),
});

type LoginFormValues = z.infer<typeof loginSchema>;

const mfaSchema = z.object({
  code: z.string().trim().min(1, "Informe o codigo."),
});

type MfaFormValues = z.infer<typeof mfaSchema>;

function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

export default function LoginPage() {
  const router = useRouter();
  const { login, verifyMfa, isAuthenticating } = useSession();
  // Presente so durante a segunda etapa (MFA habilitado para este usuario) —
  // null significa "ainda na etapa de senha".
  const [mfaChallengeToken, setMfaChallengeToken] = React.useState<string | null>(null);
  // Presente quando o login falhou por e-mail nao confirmado — troca de tela
  // em vez de um toast generico, com CTA de reenvio (mesmo tenantId/email
  // usados na tentativa de login, guardados aqui pra alimentar o reenvio).
  const [unconfirmedEmail, setUnconfirmedEmail] = React.useState<{ tenantId: string; email: string } | null>(
    null
  );
  const [isResending, setIsResending] = React.useState(false);
  // Presente quando o login falhou por bloqueio de tentativas (5 senhas
  // erradas) -- troca de tela mostrando a contagem regressiva ate poder
  // tentar de novo, em vez de deixar a pessoa martelando o formulario.
  const [lockedUntil, setLockedUntil] = React.useState<Date | null>(null);
  const [remainingSeconds, setRemainingSeconds] = React.useState(0);
  const [showPassword, setShowPassword] = React.useState(false);
  const [turnstileToken, setTurnstileToken] = React.useState<string | null>(null);
  // O token do Turnstile e de uso unico -- consumido pela API da Cloudflare
  // assim que o login e tentado, com sucesso ou nao. Incrementar a key forca
  // o widget a remontar e gerar um token novo pra proxima tentativa.
  const [turnstileNonce, setTurnstileNonce] = React.useState(0);

  function resetTurnstile() {
    setTurnstileToken(null);
    setTurnstileNonce((n) => n + 1);
  }

  React.useEffect(() => {
    if (!lockedUntil) return;

    function tick() {
      setRemainingSeconds(Math.max(0, Math.ceil((lockedUntil!.getTime() - Date.now()) / 1000)));
    }

    tick();
    const interval = setInterval(tick, 1000);
    return () => clearInterval(interval);
  }, [lockedUntil]);

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { tenantSlug: "", email: "", password: "" },
  });

  const mfaForm = useForm<MfaFormValues>({
    resolver: zodResolver(mfaSchema),
    defaultValues: { code: "" },
  });

  async function onSubmit(values: LoginFormValues) {
    let tenantId: string | undefined;
    try {
      // O login exige o Id do tenant, mas o usuario so sabe o identificador
      // publico (slug) da URL/marca do estabelecimento — resolvemos um a
      // partir do outro antes de autenticar.
      const tenant = await getTenantBySlug(values.tenantSlug);
      tenantId = tenant.id;

      if (!tenant.isActive) {
        form.setError("tenantSlug", {
          message: "Este estabelecimento esta inativo.",
        });
        return;
      }

      if (!turnstileToken) {
        return;
      }

      await login({ tenantId: tenant.id, email: values.email, password: values.password, turnstileToken });
      router.push("/painel");
    } catch (error) {
      if (error instanceof MfaRequiredError) {
        setMfaChallengeToken(error.mfaChallengeToken);
        return;
      }

      resetTurnstile();

      if (error instanceof ApiError && error.status === 404) {
        form.setError("tenantSlug", {
          message: "Nao encontramos um estabelecimento com este identificador.",
        });
        return;
      }

      // Codigo distinto do generico "senha errada": a pessoa ja provou que
      // sabe a senha, entao esconder o motivo do bloqueio nao ganha
      // seguranca nenhuma — so troca pra uma tela com CTA de reenvio.
      if (error instanceof ApiError && error.code === "Auth.EmailNotConfirmed" && tenantId) {
        setUnconfirmedEmail({ tenantId, email: values.email });
        return;
      }

      if (error instanceof ApiError && error.code === "Auth.AccountLocked" && error.lockedUntilUtc) {
        setLockedUntil(new Date(error.lockedUntilUtc));
        return;
      }

      const message =
        error instanceof ApiError ? error.message : "Nao foi possivel entrar. Tente novamente.";
      toast.error(message);
    }
  }

  async function onResendConfirmation() {
    if (!unconfirmedEmail) {
      return;
    }
    setIsResending(true);
    try {
      await resendConfirmationEmail(unconfirmedEmail);
      toast.success("E-mail reenviado. Confira sua caixa de entrada.");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Nao foi possivel reenviar o e-mail. Tente novamente.";
      toast.error(message);
    } finally {
      setIsResending(false);
    }
  }

  async function onSubmitMfa(values: MfaFormValues) {
    if (!mfaChallengeToken) {
      return;
    }

    try {
      await verifyMfa({ mfaChallengeToken, code: values.code.trim() });
      router.push("/painel");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Codigo invalido. Tente novamente.";
      toast.error(message);
    }
  }

  return (
    <main className="theme-fixed-light bg-background text-foreground grid min-h-full flex-1 lg:grid-cols-[minmax(0,7fr)_minmax(0,6fr)]">
      <LoginShowcase />

      <section className="relative flex flex-1 items-center justify-center p-6 sm:p-10 lg:p-16">
        {/* Suaviza a divisao entre os dois paineis -- em vez de uma linha
            reta 50/50, um leve degrade da cor de marca "vaza" pra dentro do
            painel claro na emenda. */}
        <div
          aria-hidden
          className="pointer-events-none absolute inset-y-0 left-0 hidden w-20 bg-gradient-to-r from-[color-mix(in_oklch,var(--primary),transparent_88%)] to-transparent lg:block"
        />

        <div className="animate-in fade-in-0 slide-in-from-bottom-3 fill-mode-both relative w-full max-w-[420px] duration-700 ease-out">
          <div className="mb-8 flex flex-col gap-2 text-lg lg:hidden">
            <Logo />
          </div>

          {lockedUntil ? (
            <>
              <div className="mb-6 flex flex-col items-center gap-3 text-center">
                <span className="bg-destructive/10 text-destructive flex size-12 items-center justify-center rounded-full">
                  <Lock className="size-6" aria-hidden />
                </span>
                <div className="space-y-1.5">
                  <h1 className="text-xl font-semibold tracking-tight">Conta temporariamente bloqueada</h1>
                  <p className="text-muted-foreground text-sm">
                    Muitas tentativas incorretas. Por segurança, aguarde antes de tentar de novo.
                  </p>
                </div>
              </div>

              <div className="border-border bg-muted/40 flex flex-col items-center gap-1 rounded-lg border py-4">
                <span className="text-muted-foreground text-xs">Tente novamente em</span>
                <span className="text-2xl font-semibold tabular-nums" aria-live="polite">
                  {formatCountdown(remainingSeconds)}
                </span>
              </div>

              {remainingSeconds <= 0 && (
                <Button
                  type="button"
                  onClick={() => setLockedUntil(null)}
                  className="mt-4 h-11 w-full text-[15px] font-semibold shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg hover:shadow-primary/25"
                >
                  Tentar novamente
                </Button>
              )}

              <p className="text-muted-foreground mt-6 text-center text-sm">
                Não quer esperar?{" "}
                <Link href="/forgot-password" className="text-primary underline-offset-4 hover:underline">
                  Redefina sua senha
                </Link>
              </p>
            </>
          ) : unconfirmedEmail ? (
            <>
              <div className="mb-6 flex flex-col items-center gap-3 text-center">
                <span className="bg-accent text-accent-foreground flex size-12 items-center justify-center rounded-full">
                  <MailCheck className="size-6" aria-hidden />
                </span>
                <div className="space-y-1.5">
                  <h1 className="text-xl font-semibold tracking-tight">Confirme seu e-mail</h1>
                  <p className="text-muted-foreground text-sm">
                    Enviamos um link de confirmação para <strong>{unconfirmedEmail.email}</strong> quando você se
                    cadastrou. Confirme para poder entrar.
                  </p>
                </div>
              </div>

              <Button
                type="button"
                onClick={onResendConfirmation}
                disabled={isResending}
                className="h-11 w-full text-[15px] font-semibold shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg hover:shadow-primary/25"
              >
                {isResending && <Loader2 className="size-4 animate-spin" aria-hidden />}
                {isResending ? "Reenviando..." : "Reenviar e-mail"}
              </Button>

              <button
                type="button"
                onClick={() => setUnconfirmedEmail(null)}
                className="text-muted-foreground mt-6 w-full text-center text-sm underline-offset-4 hover:underline"
              >
                Voltar para o login
              </button>
            </>
          ) : mfaChallengeToken ? (
            <>
              <div className="mb-6 space-y-1.5">
                <h1 className="text-xl font-semibold tracking-tight">Verificação em duas etapas</h1>
                <p className="text-muted-foreground text-sm">
                  Digite o código de 6 dígitos do seu aplicativo autenticador, ou um código de recuperação.
                </p>
              </div>

              <Form {...mfaForm}>
                <form onSubmit={mfaForm.handleSubmit(onSubmitMfa)} className="grid gap-4">
                  <FormField
                    control={mfaForm.control}
                    name="code"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Código</FormLabel>
                        <FormControl>
                          <div className="relative">
                            <ShieldCheck
                              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
                              aria-hidden
                            />
                            <Input
                              inputMode="numeric"
                              autoComplete="one-time-code"
                              autoFocus
                              placeholder="000000"
                              className={cn(LOGIN_INPUT_BASE_CLASS, "pl-10 tracking-[0.3em] tabular-nums")}
                              {...field}
                            />
                          </div>
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <Button
                    type="submit"
                    disabled={isAuthenticating}
                    className="mt-2 h-11 text-[15px] font-semibold shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg hover:shadow-primary/25"
                  >
                    {isAuthenticating && <Loader2 className="size-4 animate-spin" aria-hidden />}
                    {isAuthenticating ? "Verificando..." : "Confirmar"}
                  </Button>
                </form>
              </Form>

              <button
                type="button"
                onClick={() => setMfaChallengeToken(null)}
                className="text-muted-foreground mt-6 w-full text-center text-sm underline-offset-4 hover:underline"
              >
                Voltar para o login
              </button>
            </>
          ) : (
            <>
              <div className="mb-7 space-y-1.5">
                <h1 className="text-[1.75rem] font-bold tracking-tight">Entrar</h1>
                <p className="text-muted-foreground text-sm">
                  Acesse o painel do seu estabelecimento.
                </p>
              </div>

              <Form {...form}>
                <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-5">
                  <FormField
                    control={form.control}
                    name="tenantSlug"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Identificador do estabelecimento</FormLabel>
                        <FormControl>
                          <div className="relative">
                            <Store
                              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
                              aria-hidden
                            />
                            <Input
                              placeholder="barbearia-do-ze"
                              autoComplete="organization"
                              className={cn(LOGIN_INPUT_BASE_CLASS, "pl-10")}
                              {...field}
                            />
                          </div>
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="email"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>E-mail</FormLabel>
                        <FormControl>
                          <div className="relative">
                            <Mail
                              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
                              aria-hidden
                            />
                            <Input
                              type="email"
                              autoComplete="email"
                              className={cn(LOGIN_INPUT_BASE_CLASS, "pl-10")}
                              {...field}
                            />
                          </div>
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="password"
                    render={({ field }) => (
                      <FormItem>
                        <div className="flex items-center justify-between">
                          <FormLabel>Senha</FormLabel>
                          <Link
                            href="/forgot-password"
                            className="text-muted-foreground text-xs underline-offset-4 hover:underline"
                          >
                            Esqueceu a senha?
                          </Link>
                        </div>
                        <FormControl>
                          <div className="relative">
                            <Lock
                              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
                              aria-hidden
                            />
                            <Input
                              type={showPassword ? "text" : "password"}
                              autoComplete="current-password"
                              className={cn(LOGIN_INPUT_BASE_CLASS, "pl-10 pr-9")}
                              {...field}
                            />
                            <button
                              type="button"
                              onClick={() => setShowPassword((value) => !value)}
                              aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}
                              className="text-muted-foreground hover:text-foreground absolute inset-y-0 right-0 flex w-10 items-center justify-center"
                            >
                              {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                            </button>
                          </div>
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <TurnstileWidget key={turnstileNonce} onVerify={setTurnstileToken} onExpire={() => setTurnstileToken(null)} />
                  <Button
                    type="submit"
                    disabled={isAuthenticating || !turnstileToken}
                    className="mt-2 h-11 text-[15px] font-semibold shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg hover:shadow-primary/25"
                  >
                    {isAuthenticating && <Loader2 className="size-4 animate-spin" aria-hidden />}
                    {isAuthenticating ? "Entrando..." : "Entrar"}
                  </Button>
                </form>
              </Form>

              <p className="text-muted-foreground mt-6 text-center text-sm">
                Ainda não tem uma conta?{" "}
                <Link href="/onboarding" className="text-primary underline-offset-4 hover:underline">
                  Cadastre seu estabelecimento
                </Link>
              </p>
            </>
          )}
        </div>
      </section>
    </main>
  );
}
