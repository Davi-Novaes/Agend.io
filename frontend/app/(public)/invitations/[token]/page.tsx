"use client";

import * as React from "react";
import { use as usePromise } from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CheckCircle2 } from "lucide-react";

import { acceptInvitation, ApiError } from "@/lib/api/client";
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

const acceptSchema = z.object({
  fullName: z.string().trim().min(1, "Informe seu nome."),
  password: z.string().min(8, "A senha precisa ter ao menos 8 caracteres."),
});

type AcceptFormValues = z.infer<typeof acceptSchema>;

type PageProps = {
  params: Promise<{ token: string }>;
};

export default function AcceptInvitationPage({ params }: PageProps) {
  const { token } = usePromise(params);
  const [isAccepted, setIsAccepted] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [serverError, setServerError] = React.useState<string | null>(null);

  const form = useForm<AcceptFormValues>({
    resolver: zodResolver(acceptSchema),
    defaultValues: { fullName: "", password: "" },
  });

  async function onSubmit(values: AcceptFormValues) {
    setIsSubmitting(true);
    setServerError(null);
    try {
      await acceptInvitation({ token, fullName: values.fullName, password: values.password });
      setIsAccepted(true);
    } catch (error) {
      setServerError(
        error instanceof ApiError ? error.message : "Nao foi possivel aceitar o convite. Tente novamente."
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isAccepted) {
    return (
      <main className="flex min-h-full flex-1 flex-col items-center justify-center gap-3 p-4 text-center">
        <CheckCircle2 className="text-primary size-10" strokeWidth={1.5} />
        <h1 className="text-xl font-semibold tracking-tight">Conta criada!</h1>
        <p className="text-muted-foreground max-w-sm text-sm">
          Ja pode entrar com o e-mail que recebeu o convite e a senha que voce acabou de definir.
        </p>
        <Button asChild className="mt-2">
          <Link href="/login">Ir para o login</Link>
        </Button>
      </main>
    );
  }

  return (
    <main className="flex min-h-full flex-1 items-center justify-center p-4 sm:p-6">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex justify-center">
          <Logo />
        </div>

        <div className="mb-6 space-y-1.5 text-center">
          <h1 className="text-xl font-semibold tracking-tight">Aceitar convite</h1>
          <p className="text-muted-foreground text-sm">Defina seu nome e uma senha para acessar o painel.</p>
        </div>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4">
            <FormField
              control={form.control}
              name="fullName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Seu nome</FormLabel>
                  <FormControl>
                    <Input autoComplete="name" autoFocus {...field} />
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
                    <Input type="password" autoComplete="new-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            {serverError && <p className="text-destructive text-sm">{serverError}</p>}
            <Button type="submit" disabled={isSubmitting} className="mt-2 h-10">
              {isSubmitting ? "Criando conta..." : "Criar conta"}
            </Button>
          </form>
        </Form>
      </div>
    </main>
  );
}
