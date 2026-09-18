"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { CheckCircle2, Lock } from "lucide-react";

import { resetPassword, ApiError } from "@/lib/api/client";
import { strongPasswordSchema } from "@/lib/validation/password";
import { Logo } from "@/components/logo";
import { LoginShowcase } from "@/components/auth/login-showcase";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

// Mesmo estilo de input da tela de login (ver login/page.tsx) -- pedido
// explicito do usuario pra esta tela (e a de forgot-password) ficarem
// visualmente identicas ao login, nao uma versao "simplificada" a parte.
const LOGIN_INPUT_BASE_CLASS =
  "h-11 rounded-xl border-border bg-card text-[15px] shadow-xs transition-all duration-200 hover:border-foreground/25 focus-visible:ring-4 focus-visible:ring-primary/15";

const resetPasswordSchema = z
  .object({
    newPassword: strongPasswordSchema,
    confirmPassword: z.string().min(1, "Confirme a nova senha."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "As senhas não coincidem.",
    path: ["confirmPassword"],
  });

type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export default function ResetPasswordPage() {
  const params = useParams<{ token: string }>();
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [done, setDone] = React.useState(false);

  const form = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });

  async function onSubmit(values: ResetPasswordFormValues) {
    setIsSubmitting(true);
    try {
      await resetPassword({ token: params.token, newPassword: values.newPassword });
      setDone(true);
    } catch (error) {
      const message =
        error instanceof ApiError
          ? error.code === "Auth.PasswordResetTokenInvalid"
            ? "Este link expirou ou já foi usado. Peça um novo."
            : error.message
          : "Não foi possível redefinir a senha. Tente novamente.";
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

          {done ? (
            <>
              <div className="mb-6 flex flex-col items-center gap-3 text-center">
                <span className="bg-accent text-accent-foreground flex size-12 items-center justify-center rounded-full">
                  <CheckCircle2 className="size-6" aria-hidden />
                </span>
                <div className="space-y-1.5">
                  <h1 className="text-xl font-semibold tracking-tight">Senha redefinida</h1>
                  <p className="text-muted-foreground text-sm">
                    Sua senha foi alterada e todas as sessões ativas foram encerradas. Entre novamente com a nova
                    senha.
                  </p>
                </div>
              </div>

              <Button
                type="button"
                className="h-11 w-full text-[15px] font-semibold shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg hover:shadow-primary/25"
                onClick={() => router.push("/login")}
              >
                Ir para o login
              </Button>
            </>
          ) : (
            <>
              <div className="mb-7 space-y-1.5">
                <h1 className="text-[1.75rem] font-bold tracking-tight">Escolha uma nova senha</h1>
                <p className="text-muted-foreground text-sm">
                  Mínimo de 10 caracteres, com pelo menos 1 letra maiúscula e 1 símbolo.
                </p>
              </div>

              <Form {...form}>
                <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-5">
                  <FormField
                    control={form.control}
                    name="newPassword"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Nova senha</FormLabel>
                        <FormControl>
                          <div className="relative">
                            <Lock
                              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
                              aria-hidden
                            />
                            <Input
                              type="password"
                              autoComplete="new-password"
                              autoFocus
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
                    name="confirmPassword"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Confirme a nova senha</FormLabel>
                        <FormControl>
                          <div className="relative">
                            <Lock
                              className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
                              aria-hidden
                            />
                            <Input
                              type="password"
                              autoComplete="new-password"
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
                    {isSubmitting ? "Salvando..." : "Redefinir senha"}
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
