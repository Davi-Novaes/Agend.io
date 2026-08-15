"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import {
  getTenantProfile,
  updateTenantPaymentSettings,
  ApiError,
  type TenantProfile,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Input } from "@/components/ui/input";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const settingsSchema = z.object({
  paymentRequired: z.boolean(),
  depositPercentage: z.coerce.number().int().min(1, "Precisa ser pelo menos 1%.").max(100, "No maximo 100%."),
});

// z.coerce faz o tipo de entrada (antes da validacao) divergir do de saida
// (depois da coercao) — mesmo padrao de settings/loyalty/page.tsx.
type SettingsFormValues = z.output<typeof settingsSchema>;
type SettingsFormInput = z.input<typeof settingsSchema>;

export default function PaymentSettingsPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";

  const profileQuery = useQuery({
    queryKey: ["tenant", "profile"],
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });

  if (!session) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6">
      <p className="text-muted-foreground text-sm">
        Desligado por padrão — o cliente agenda pelo portal público sem pagar nada. Se ativado, o cliente precisa
        pagar um sinal (via PIX, Asaas) e informar o CPF para confirmar o agendamento.
      </p>

      {profileQuery.isLoading || !profileQuery.data ? (
        <Skeleton className="h-64 w-full" />
      ) : (
        <PaymentSettingsCard profile={profileQuery.data} accessToken={accessToken} />
      )}
    </div>
  );
}

function PaymentSettingsCard({ profile, accessToken }: { profile: TenantProfile; accessToken: string }) {
  const queryClient = useQueryClient();

  const form = useForm<SettingsFormInput, unknown, SettingsFormValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: {
      paymentRequired: profile.paymentRequired,
      depositPercentage: profile.depositPercentage,
    },
  });

  const mutation = useMutation({
    mutationFn: (values: SettingsFormValues) => updateTenantPaymentSettings(values, accessToken),
    onSuccess: () => {
      toast.success("Configurações de pagamento atualizadas.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar as configurações."),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Sinal no agendamento público</CardTitle>
        <CardDescription>
          O percentual é sobre o preço do serviço — use 100% para exigir o pagamento integral antecipado.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
            <FormField
              control={form.control}
              name="paymentRequired"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between gap-4">
                  <FormLabel>Exigir sinal para confirmar o agendamento</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} aria-label="Exigir sinal para confirmar o agendamento" />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="depositPercentage"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Percentual do sinal</FormLabel>
                  <FormControl>
                    <Input type="number" min={1} max={100} {...field} value={field.value as number} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <Button type="submit" disabled={mutation.isPending} className="mt-2 w-fit">
              {mutation.isPending ? "Salvando..." : "Salvar pagamento"}
            </Button>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
