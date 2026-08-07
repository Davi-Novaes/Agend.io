"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ArrowLeft } from "lucide-react";

import {
  listPlans,
  getMySubscription,
  subscribeToPlan,
  cancelSubscription,
  ApiError,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const subscribeSchema = z.object({
  fullName: z.string().min(1, "Informe o nome."),
  cpfCnpj: z.string().min(11, "Informe um CPF ou CNPJ valido."),
  email: z.union([z.email("E-mail invalido."), z.literal("")]),
});

type SubscribeFormValues = z.infer<typeof subscribeSchema>;

const STATUS_LABELS: Record<string, string> = {
  Trialing: "Em teste gratis",
  Active: "Ativa",
  PastDue: "Pagamento atrasado",
  Canceled: "Cancelada",
};

function daysUntil(isoDate: string): number {
  return Math.ceil((new Date(isoDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24));
}

export default function BillingSettingsPage() {
  const router = useRouter();
  const { session } = useSession();
  const queryClient = useQueryClient();

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const subscriptionQuery = useQuery({
    queryKey: ["billing", "subscription"],
    queryFn: () => getMySubscription(accessToken),
    enabled: Boolean(session),
  });

  const plansQuery = useQuery({
    queryKey: ["billing", "plans"],
    queryFn: () => listPlans(accessToken),
    enabled: Boolean(session),
  });

  const form = useForm<SubscribeFormValues>({
    resolver: zodResolver(subscribeSchema),
    defaultValues: { fullName: "", cpfCnpj: "", email: "" },
  });

  const subscribeMutation = useMutation({
    mutationFn: (values: SubscribeFormValues) => {
      const planId = plansQuery.data?.[0]?.id;
      if (!planId) {
        throw new Error("Nenhum plano disponivel.");
      }
      return subscribeToPlan(
        { planId, fullName: values.fullName, cpfCnpj: values.cpfCnpj, email: values.email || undefined },
        accessToken
      );
    },
    onSuccess: (result) => {
      toast.success("Assinatura iniciada — complete o pagamento na pagina que abriu.");
      if (result.invoiceUrl) {
        window.open(result.invoiceUrl, "_blank", "noopener,noreferrer");
      }
      queryClient.invalidateQueries({ queryKey: ["billing", "subscription"] });
    },
    onError: (error: unknown) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel assinar o plano.");
    },
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelSubscription(accessToken),
    onSuccess: () => {
      toast.success("Assinatura cancelada.");
      queryClient.invalidateQueries({ queryKey: ["billing", "subscription"] });
    },
    onError: (error: unknown) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cancelar a assinatura.");
    },
  });

  if (!session) {
    return null;
  }

  const subscription = subscriptionQuery.data;
  const plan = plansQuery.data?.[0];

  return (
    <main className="mx-auto flex min-h-full w-full max-w-lg flex-1 flex-col p-6 sm:p-10">
      <Link href="/painel" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <h1 className="text-xl font-semibold tracking-tight">Plano e assinatura</h1>
      <p className="text-muted-foreground mt-1 mb-6 text-sm">Acompanhe o status do seu plano no Agendio.</p>

      {subscriptionQuery.isLoading ? (
        <p className="text-muted-foreground text-sm">Carregando...</p>
      ) : subscription ? (
        <Card className="mb-6">
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle>{subscription.planName}</CardTitle>
              <Badge variant={subscription.status === "Active" ? "default" : "secondary"}>
                {STATUS_LABELS[subscription.status] ?? subscription.status}
              </Badge>
            </div>
            {subscription.status === "Trialing" && (
              <CardDescription>
                {Math.max(daysUntil(subscription.trialEndsAtUtc), 0)} dia(s) restantes de teste gratis.
              </CardDescription>
            )}
          </CardHeader>
          <CardContent className="space-y-3">
            {subscription.latestPayment && (
              <div className="text-sm">
                <p>
                  Ultimo pagamento: <span className="font-medium">{subscription.latestPayment.status}</span>
                  {" — "}R$ {subscription.latestPayment.amount.toFixed(2)}
                </p>
                {subscription.latestPayment.invoiceUrl && subscription.latestPayment.status !== "Confirmed" && (
                  <a
                    href={subscription.latestPayment.invoiceUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary underline-offset-4 hover:underline"
                  >
                    Ver fatura
                  </a>
                )}
              </div>
            )}

            {subscription.status === "Active" ? (
              <Button
                variant="outline"
                disabled={cancelMutation.isPending}
                onClick={() => cancelMutation.mutate()}
              >
                {cancelMutation.isPending ? "Cancelando..." : "Cancelar assinatura"}
              </Button>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {subscription && subscription.status !== "Active" && plan && (
        <Card>
          <CardHeader>
            <CardTitle>Assinar {plan.name}</CardTitle>
            <CardDescription>
              R$ {plan.priceAmount.toFixed(2)}/mes — pague com PIX, boleto ou cartao.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Form {...form}>
              <form onSubmit={form.handleSubmit((values) => subscribeMutation.mutate(values))} className="grid gap-4">
                <FormField
                  control={form.control}
                  name="fullName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Nome completo</FormLabel>
                      <FormControl>
                        <Input autoComplete="name" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="cpfCnpj"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>CPF ou CNPJ</FormLabel>
                      <FormControl>
                        <Input inputMode="numeric" {...field} />
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
                      <FormLabel>E-mail (opcional)</FormLabel>
                      <FormControl>
                        <Input type="email" autoComplete="email" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <Button type="submit" disabled={subscribeMutation.isPending} className="mt-2 w-fit">
                  {subscribeMutation.isPending ? "Processando..." : "Assinar agora"}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      )}
    </main>
  );
}
