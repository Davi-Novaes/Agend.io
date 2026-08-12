"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import { getTenantBySlug, ApiError } from "@/lib/api/client";
import { useSession, MfaRequiredError } from "@/lib/auth/session-context";
import { Logo } from "@/components/logo";
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

export default function LoginPage() {
  const router = useRouter();
  const { login, verifyMfa, isAuthenticating } = useSession();
  // Presente so durante a segunda etapa (MFA habilitado para este usuario) —
  // null significa "ainda na etapa de senha".
  const [mfaChallengeToken, setMfaChallengeToken] = React.useState<string | null>(null);

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { tenantSlug: "", email: "", password: "" },
  });

  const mfaForm = useForm<MfaFormValues>({
    resolver: zodResolver(mfaSchema),
    defaultValues: { code: "" },
  });

  async function onSubmit(values: LoginFormValues) {
    try {
      // O login exige o Id do tenant, mas o usuario so sabe o identificador
      // publico (slug) da URL/marca do estabelecimento — resolvemos um a
      // partir do outro antes de autenticar.
      const tenant = await getTenantBySlug(values.tenantSlug);

      if (!tenant.isActive) {
        form.setError("tenantSlug", {
          message: "Este estabelecimento esta inativo.",
        });
        return;
      }

      await login({ tenantId: tenant.id, email: values.email, password: values.password });
      router.push("/painel");
    } catch (error) {
      if (error instanceof MfaRequiredError) {
        setMfaChallengeToken(error.mfaChallengeToken);
        return;
      }

      if (error instanceof ApiError && error.status === 404) {
        form.setError("tenantSlug", {
          message: "Nao encontramos um estabelecimento com este identificador.",
        });
        return;
      }

      const message =
        error instanceof ApiError ? error.message : "Nao foi possivel entrar. Tente novamente.";
      toast.error(message);
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
    <main className="grid min-h-full flex-1 lg:grid-cols-2">
      <section className="relative hidden flex-col justify-between overflow-hidden bg-primary p-10 text-primary-foreground lg:flex">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,color-mix(in_oklch,var(--primary-foreground),transparent_82%),transparent_55%),radial-gradient(circle_at_85%_80%,color-mix(in_oklch,var(--primary-foreground),transparent_88%),transparent_60%)]"
        />
        <Logo inverted className="relative text-primary-foreground" />
        <blockquote className="relative space-y-3">
          <p className="text-2xl leading-snug font-medium text-balance">
            Agenda, clientes e equipe em um só lugar — sem complicar a rotina do seu negócio.
          </p>
          <footer className="text-sm text-primary-foreground/70">
            Barbearias, clínicas, pet shops, estúdios e muito mais.
          </footer>
        </blockquote>
      </section>

      <section className="flex flex-1 items-center justify-center p-6 sm:p-10">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex flex-col gap-2 lg:hidden">
            <Logo />
          </div>

          {mfaChallengeToken ? (
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
                          <Input
                            inputMode="numeric"
                            autoComplete="one-time-code"
                            autoFocus
                            placeholder="000000"
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <Button type="submit" disabled={isAuthenticating} className="mt-2 h-10">
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
              <div className="mb-6 space-y-1.5">
                <h1 className="text-xl font-semibold tracking-tight">Entrar</h1>
                <p className="text-muted-foreground text-sm">
                  Acesse o painel do seu estabelecimento.
                </p>
              </div>

              <Form {...form}>
                <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4">
                  <FormField
                    control={form.control}
                    name="tenantSlug"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Identificador do estabelecimento</FormLabel>
                        <FormControl>
                          <Input placeholder="barbearia-do-ze" autoComplete="organization" {...field} />
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
                          <Input type="email" autoComplete="email" {...field} />
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
                        <FormLabel>Senha</FormLabel>
                        <FormControl>
                          <Input type="password" autoComplete="current-password" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <Button type="submit" disabled={isAuthenticating} className="mt-2 h-10">
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
