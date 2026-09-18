"use client";

import * as React from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Mail, MailCheck, Store } from "lucide-react";

import { getTenantBySlug, forgotPassword, ApiError } from "@/lib/api/client";
import { Logo } from "@/components/logo";
import { LoginShowcase } from "@/components/auth/login-showcase";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

// Mesmo estilo de input da tela de login (ver login/page.tsx) -- pedido
// explicito do usuario pra esta tela (e a de reset-password) ficarem
// visualmente identicas ao login, nao uma versao "simplificada" a parte.
const LOGIN_INPUT_BASE_CLASS =
  "h-11 rounded-xl border-border bg-card text-[15px] shadow-xs transition-all duration-200 hover:border-foreground/25 focus-visible:ring-4 focus-visible:ring-primary/15";

const forgotPasswordSchema = z.object({
  tenantSlug: z.string().trim().min(1, "Informe o identificador do estabelecimento."),
  email: z.email("Informe um e-mail valido."),
});

type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export default function ForgotPasswordPage() {
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [sent, setSent] = React.useState(false);

  const form = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { tenantSlug: "", email: "" },
  });

  async function onSubmit(values: ForgotPasswordFormValues) {
    setIsSubmitting(true);
    try {
      // O endpoint precisa do Id do tenant, mas a pessoa so sabe o
      // identificador publico (slug) — mesma resolucao da tela de login.
      const tenant = await getTenantBySlug(values.tenantSlug);
      await forgotPassword({ tenantId: tenant.id, email: values.email });
      // Sempre mostra a mesma tela de sucesso, exista ou nao a conta — o
      // backend ja responde 204 genérico de proposito (evita enumeração).
      setSent(true);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        form.setError("tenantSlug", { message: "Nao encontramos um estabelecimento com este identificador." });
        return;
      }
      const message = error instanceof ApiError ? error.message : "Nao foi possivel enviar o e-mail. Tente novamente.";
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="theme-fixed-light bg-background text-foreground grid min-h-full flex-1 lg:grid-cols-[minmax(0,7fr)_minmax(0,6fr)]">
      <LoginShowcase />

      <section className="relative flex flex-1 items-center justify-center p-6 sm:p-10 lg:p-16">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-y-0 left-0 hidden w-20 bg-gradient-to-r from-[color-mix(in_oklch,var(--primary),transparent_88%)] to-transparent lg:block"
        />

        <div className="animate-in fade-in-0 slide-in-from-bottom-3 fill-mode-both relative w-full max-w-[420px] duration-700 ease-out">
          <div className="mb-8 flex flex-col gap-2 text-lg lg:hidden">
            <Logo />
          </div>

          {sent ? (
            <>
              <div className="mb-6 flex flex-col items-center gap-3 text-center">
                <span className="bg-accent text-accent-foreground flex size-12 items-center justify-center rounded-full">
                  <MailCheck className="size-6" aria-hidden />
                </span>
                <div className="space-y-1.5">
                  <h1 className="text-xl font-semibold tracking-tight">Verifique seu e-mail</h1>
                  <p className="text-muted-foreground text-sm">
                    Se existir uma conta com esses dados, enviamos um link para redefinir a senha. O link expira em
                    30 minutos.
                  </p>
                </div>
              </div>

              <Link
                href="/login"
                className="text-muted-foreground block w-full text-center text-sm underline-offset-4 hover:underline"
              >
                Voltar para o login
              </Link>
            </>
          ) : (
            <>
              <div className="mb-7 space-y-1.5">
                <h1 className="text-[1.75rem] font-bold tracking-tight">Esqueceu sua senha?</h1>
                <p className="text-muted-foreground text-sm">
                  Informe o identificador do estabelecimento e o e-mail da sua conta.
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
                  <Button
                    type="submit"
                    disabled={isSubmitting}
                    className="mt-2 h-11 text-[15px] font-semibold shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg hover:shadow-primary/25"
                  >
                    {isSubmitting ? "Enviando..." : "Enviar link de redefinição"}
                  </Button>
                </form>
              </Form>

              <Link
                href="/login"
                className="text-muted-foreground mt-6 block w-full text-center text-sm underline-offset-4 hover:underline"
              >
                Voltar para o login
              </Link>
            </>
          )}
        </div>
      </section>
    </main>
  );
}
