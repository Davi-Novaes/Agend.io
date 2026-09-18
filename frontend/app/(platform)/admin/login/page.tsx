"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import { ApiError } from "@/lib/api/client";
import { usePlatformSession, PlatformMfaRequiredError } from "@/lib/auth/platform-session-context";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const loginSchema = z.object({
  email: z.email("Informe um e-mail valido."),
  password: z.string().min(1, "Informe a senha."),
});

type LoginFormValues = z.infer<typeof loginSchema>;

const mfaSchema = z.object({
  code: z.string().trim().min(1, "Informe o codigo."),
});

type MfaFormValues = z.infer<typeof mfaSchema>;

export default function PlatformAdminLoginPage() {
  const router = useRouter();
  const { login, verifyMfa, isAuthenticating } = usePlatformSession();
  // Presente so durante a segunda etapa (MFA habilitado para este admin) —
  // mesmo padrao da tela de login do tenant.
  const [mfaChallengeToken, setMfaChallengeToken] = React.useState<string | null>(null);

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  const mfaForm = useForm<MfaFormValues>({
    resolver: zodResolver(mfaSchema),
    defaultValues: { code: "" },
  });

  async function onSubmit(values: LoginFormValues) {
    try {
      await login(values);
      router.push("/admin");
    } catch (error) {
      if (error instanceof PlatformMfaRequiredError) {
        setMfaChallengeToken(error.mfaChallengeToken);
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
      router.push("/admin");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Codigo invalido. Tente novamente.";
      toast.error(message);
    }
  }

  return (
    <main className="flex min-h-full flex-1 items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <Logo />
          </CardTitle>
          <CardDescription>
            {mfaChallengeToken
              ? "Digite o código de verificação em duas etapas."
              : "Acesso restrito ao Super Admin da plataforma."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {mfaChallengeToken ? (
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
                <button
                  type="button"
                  onClick={() => setMfaChallengeToken(null)}
                  className="text-muted-foreground text-center text-sm underline-offset-4 hover:underline"
                >
                  Voltar para o login
                </button>
              </form>
            </Form>
          ) : (
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4">
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
          )}
        </CardContent>
      </Card>
    </main>
  );
}
