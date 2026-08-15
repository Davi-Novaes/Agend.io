"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import {
  getTenantProfile,
  updateTenantLoyaltySettings,
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
  loyaltyProgramEnabled: z.boolean(),
  loyaltyVisitsForReward: z.coerce.number().int().min(1, "Precisa ser pelo menos 1 visita."),
  loyaltyRewardDescription: z.string().trim().min(1, "Descreva a recompensa.").max(200),
});

// z.coerce faz o tipo de entrada (antes da validacao) divergir do de saida
// (depois da coercao) — o form precisa ser tipado com os dois generics do RHF
// para o resolver aceitar valores brutos de <input> e devolver numeros no submit.
type SettingsFormValues = z.output<typeof settingsSchema>;
type SettingsFormInput = z.input<typeof settingsSchema>;

export default function LoyaltySettingsPage() {
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
        Recompense clientes recorrentes: a cada visita concluída eles ganham 1 ponto automaticamente. Ao atingir o
        número de visitas configurado, o cliente pode resgatar a recompensa.
      </p>

      {profileQuery.isLoading || !profileQuery.data ? (
        <Skeleton className="h-64 w-full" />
      ) : (
        <LoyaltySettingsCard profile={profileQuery.data} accessToken={accessToken} />
      )}
    </div>
  );
}

function LoyaltySettingsCard({ profile, accessToken }: { profile: TenantProfile; accessToken: string }) {
  const queryClient = useQueryClient();

  const form = useForm<SettingsFormInput, unknown, SettingsFormValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: {
      loyaltyProgramEnabled: profile.loyaltyProgramEnabled,
      loyaltyVisitsForReward: profile.loyaltyVisitsForReward,
      loyaltyRewardDescription: profile.loyaltyRewardDescription,
    },
  });

  const mutation = useMutation({
    mutationFn: (values: SettingsFormValues) => updateTenantLoyaltySettings(values, accessToken),
    onSuccess: () => {
      toast.success("Programa de fidelidade atualizado.");
      queryClient.invalidateQueries({ queryKey: ["tenant", "profile"] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar as configurações."),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Programa de fidelidade</CardTitle>
        <CardDescription>Ligado por padrão — 1 ponto por visita concluída, 10 visitas para a recompensa.</CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
            <FormField
              control={form.control}
              name="loyaltyProgramEnabled"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between gap-4">
                  <FormLabel>Programa de fidelidade ativo</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} aria-label="Programa de fidelidade ativo" />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="loyaltyVisitsForReward"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Visitas para a recompensa</FormLabel>
                  <FormControl>
                    <Input type="number" min={1} {...field} value={field.value as number} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="loyaltyRewardDescription"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Descrição da recompensa</FormLabel>
                  <FormControl>
                    <Input placeholder="Ex.: Corte grátis, 10% de desconto..." {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <Button type="submit" disabled={mutation.isPending} className="mt-2 w-fit">
              {mutation.isPending ? "Salvando..." : "Salvar fidelidade"}
            </Button>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
