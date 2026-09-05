"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";

import { getTenantProfile, updateTenantCompanyInfo, TENANT_PROFILE_QUERY_KEY, ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtRole } from "@/lib/auth/decode-jwt";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

const companySchema = z.object({
  name: z.string().trim().min(1, "Informe o nome do estabelecimento."),
  legalName: z.string(),
  document: z.string(),
  city: z.string(),
  state: z.string(),
  zipCode: z.string(),
});
type CompanyFormValues = z.infer<typeof companySchema>;

export default function CompanySettingsPage() {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";
  const queryClient = useQueryClient();
  // Escrita e Owner-only tambem no backend (RequireRole("Owner") em
  // PUT /api/tenants/company-info) -- isto so evita que Staff veja um
  // formulario normal e descubra o bloqueio com um 403 confuso ao salvar.
  const isOwner = session ? decodeJwtRole(session.accessToken) === "Owner" : false;

  const profileQuery = useQuery({
    queryKey: TENANT_PROFILE_QUERY_KEY,
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });

  const form = useForm<CompanyFormValues>({
    resolver: zodResolver(companySchema),
    values: profileQuery.data
      ? {
          name: profileQuery.data.name,
          legalName: profileQuery.data.legalName ?? "",
          document: profileQuery.data.document ?? "",
          city: profileQuery.data.city ?? "",
          state: profileQuery.data.state ?? "",
          zipCode: profileQuery.data.zipCode ?? "",
        }
      : undefined,
    defaultValues: { name: "", legalName: "", document: "", city: "", state: "", zipCode: "" },
  });

  const mutation = useMutation({
    mutationFn: (values: CompanyFormValues) =>
      updateTenantCompanyInfo(
        {
          name: values.name.trim(),
          legalName: toNullable(values.legalName),
          document: toNullable(values.document),
          city: toNullable(values.city),
          state: toNullable(values.state),
          zipCode: toNullable(values.zipCode),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Dados da empresa atualizados.");
      queryClient.invalidateQueries({ queryKey: TENANT_PROFILE_QUERY_KEY });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível salvar."),
  });

  return (
    <div className="flex w-full max-w-2xl flex-1 flex-col gap-6">
      <div>
        <h1 className="text-lg font-semibold">Dados da empresa</h1>
        <p className="text-muted-foreground text-sm">Informações cadastrais do estabelecimento — nunca aparecem na página pública.</p>
        {!isOwner && (
          <p className="text-muted-foreground mt-1 text-sm">Somente o administrador da conta pode alterar estes dados.</p>
        )}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Cadastro</CardTitle>
          <CardDescription>Nome, razão social, CNPJ/CPF e localização do estabelecimento.</CardDescription>
        </CardHeader>
        <CardContent>
          {profileQuery.isLoading ? (
            <div className="flex max-w-md flex-col gap-3">
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
            </div>
          ) : (
            <Form {...form}>
              <fieldset disabled={!isOwner} className="contents">
                <form onSubmit={form.handleSubmit((values) => mutation.mutate(values))} className="grid max-w-md gap-4">
                  <FormField
                    control={form.control}
                    name="name"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Nome do estabelecimento</FormLabel>
                        <FormControl>
                          <Input {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="legalName"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Razão social (opcional)</FormLabel>
                        <FormControl>
                          <Input placeholder="Ex.: José da Silva Serviços Ltda" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="document"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>CNPJ/CPF (opcional)</FormLabel>
                        <FormControl>
                          <Input placeholder="00.000.000/0000-00" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <div className="grid grid-cols-[1fr_auto_auto] gap-3">
                    <FormField
                      control={form.control}
                      name="city"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Cidade</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="state"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>UF</FormLabel>
                          <FormControl>
                            <Input maxLength={2} className="w-16 uppercase" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="zipCode"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>CEP</FormLabel>
                          <FormControl>
                            <Input placeholder="00000-000" className="w-28" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>
                  <p className="text-muted-foreground text-xs">
                    Telefone, e-mail e endereço exibidos publicamente ficam em Marca → Informações.
                  </p>
                  <Button type="submit" disabled={mutation.isPending} className="w-fit">
                    {mutation.isPending ? "Salvando..." : "Salvar dados da empresa"}
                  </Button>
                </form>
              </fieldset>
            </Form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
