"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import { ApiError } from "@/lib/api/client";
import { usePlatformSession } from "@/lib/auth/platform-session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const loginSchema = z.object({
  email: z.email("Informe um e-mail valido."),
  password: z.string().min(1, "Informe a senha."),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export default function PlatformAdminLoginPage() {
  const router = useRouter();
  const { login, isAuthenticating } = usePlatformSession();

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  async function onSubmit(values: LoginFormValues) {
    try {
      await login(values);
      router.push("/admin");
    } catch (error) {
      const message =
        error instanceof ApiError ? error.message : "Nao foi possivel entrar. Tente novamente.";
      toast.error(message);
    }
  }

  return (
    <main className="flex min-h-full flex-1 items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Agendio Platform</CardTitle>
          <CardDescription>Acesso restrito ao Super Admin da plataforma.</CardDescription>
        </CardHeader>
        <CardContent>
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
        </CardContent>
      </Card>
    </main>
  );
}
