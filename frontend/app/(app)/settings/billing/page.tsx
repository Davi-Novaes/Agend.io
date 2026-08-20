"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import {
  listPlans,
  getMySubscription,
  subscribeToPlan,
  cancelSubscription,
  activateFreePlan,
  ApiError,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";

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

const STATUS_VARIANTS: Record<string, "success" | "info" | "destructive" | "secondary"> = {
  Trialing: "info",
  Active: "success",
  PastDue: "destructive",
  Canceled: "secondary",
};

function daysUntil(isoDate: string): number {
  return Math.ceil((new Date(isoDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24));
}

export default function BillingSettingsPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();
  // Cancelamento de assinatura paga e destrutivo (receita perdida, sem
  // desfazer) — precisa do mesmo AlertDialog de confirmacao ja usado no
  // Financeiro para acoes equivalentes (BL-02, docs/BACKLOG.md).
  const [confirmingCancel, setConfirmingCancel] = React.useState(false);

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
      setConfirmingCancel(false);
      queryClient.invalidateQueries({ queryKey: ["billing", "subscription"] });
    },
    onError: (error: unknown) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cancelar a assinatura.");
    },
  });

  // Plano Free nao pode passar pelo formulario de plano pago (CPF/CNPJ +
  // texto de pagamento nao fazem sentido pra um plano sem cobranca, e o
  // submit tentava criar uma assinatura de R$0 na Asaas) — endpoint proprio,
  // sem Asaas (BL-23, docs/BACKLOG.md).
  const activateFreeMutation = useMutation({
    mutationFn: () => activateFreePlan(accessToken),
    onSuccess: () => {
      toast.success("Plano Gratis ativado.");
      queryClient.invalidateQueries({ queryKey: ["billing", "subscription"] });
    },
    onError: (error: unknown) => {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel ativar o plano Gratis.");
    },
  });

  const subscription = subscriptionQuery.data;
  const plan = plansQuery.data?.[0];
  const isFreePlan = plan?.priceAmount === 0;

  return (
    <div className="mx-auto flex w-full max-w-lg flex-1 flex-col">
      <p className="text-muted-foreground mb-6 text-sm">Acompanhe o status do seu plano no Agendio.</p>

      {subscriptionQuery.isLoading ? (
        <Card className="mb-6">
          <CardHeader>
            <Skeleton className="h-6 w-40" />
          </CardHeader>
          <CardContent>
            <Skeleton className="h-4 w-full" />
          </CardContent>
        </Card>
      ) : subscription ? (
        <Card className="mb-6">
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle>{subscription.planName}</CardTitle>
              <Badge variant={STATUS_VARIANTS[subscription.status] ?? "secondary"}>
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
                onClick={() => setConfirmingCancel(true)}
              >
                {cancelMutation.isPending ? "Cancelando..." : "Cancelar assinatura"}
              </Button>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {subscription && subscription.status !== "Active" && plan && isFreePlan && (
        <Card>
          <CardHeader>
            <CardTitle>Ativar {plan.name}</CardTitle>
            <CardDescription>Sem cartao, sem compromisso — comece a usar agora.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button
              onClick={() => activateFreeMutation.mutate()}
              disabled={activateFreeMutation.isPending}
              className="w-fit"
            >
              {activateFreeMutation.isPending ? "Ativando..." : `Ativar ${plan.name}`}
            </Button>
          </CardContent>
        </Card>
      )}

      {subscription && subscription.status !== "Active" && plan && !isFreePlan && (
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

      <AlertDialog open={confirmingCancel} onOpenChange={setConfirmingCancel}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancelar assinatura?</AlertDialogTitle>
            <AlertDialogDescription>
              {subscription &&
                `Isso cancela imediatamente a assinatura do plano "${subscription.planName}". Essa acao nao pode ser desfeita — para voltar a usar um plano pago, sera preciso assinar novamente.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Voltar</AlertDialogCancel>
            <AlertDialogAction variant="destructive" disabled={cancelMutation.isPending} onClick={() => cancelMutation.mutate()}>
              {cancelMutation.isPending ? "Cancelando..." : "Cancelar assinatura"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
