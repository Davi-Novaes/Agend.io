"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Building2, Plus } from "lucide-react";

import { listUnits, createUnit, updateUnit, setUnitActiveStatus, ApiError, type UnitSummary } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtRole } from "@/lib/auth/decode-jwt";
import { BRAZILIAN_STATES, COUNTRIES } from "@/lib/brazilian-states";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

const NO_STATE = "__none__";

const unitSchema = z.object({
  name: z.string().min(1, "Informe o nome."),
  address: z.string(),
  city: z.string(),
  state: z.string(),
  country: z.string(),
});

type UnitFormValues = z.infer<typeof unitSchema>;
const emptyUnitForm: UnitFormValues = { name: "", address: "", city: "", state: "", country: "Brasil" };

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

function formatLocation(unit: UnitSummary): string {
  const cityState = [unit.city, unit.state].filter(Boolean).join(" / ");
  return [unit.address, cityState].filter(Boolean).join(" — ") || "—";
}

export default function UnitsSettingsPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();
  // Escrita (criar/editar/ativar unidade) e Owner-only no backend (ver
  // TenancyEndpoints) -- a leitura foi liberada pra qualquer papel (Sidebar/
  // Header/tema dependem dela), entao sem isto Staff veria os botoes normais
  // e so descobriria o bloqueio com um 403 confuso ao tentar salvar.
  const isOwner = session ? decodeJwtRole(session.accessToken) === "Owner" : false;

  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingUnit, setEditingUnit] = React.useState<UnitSummary | null>(null);

  const accessToken = session?.accessToken ?? "";

  const unitsQuery = useQuery({
    queryKey: ["units"],
    queryFn: () => listUnits(accessToken),
    enabled: Boolean(session),
  });

  const form = useForm<UnitFormValues>({
    resolver: zodResolver(unitSchema),
    defaultValues: emptyUnitForm,
  });

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: ["units"] });

  const createMutation = useMutation({
    mutationFn: (values: UnitFormValues) =>
      createUnit(
        {
          name: values.name,
          address: toNullable(values.address),
          city: toNullable(values.city),
          state: values.state === NO_STATE ? null : toNullable(values.state),
          country: toNullable(values.country),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Unidade cadastrada.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cadastrar a unidade."),
  });

  const updateMutation = useMutation({
    mutationFn: (values: UnitFormValues) => {
      if (!editingUnit) {
        throw new Error("Nenhuma unidade selecionada.");
      }
      return updateUnit(
        editingUnit.id,
        {
          name: values.name,
          address: toNullable(values.address),
          city: toNullable(values.city),
          state: values.state === NO_STATE ? null : toNullable(values.state),
          country: toNullable(values.country),
        },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Unidade atualizada.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar a unidade."),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setUnitActiveStatus(id, isActive, accessToken),
    onSuccess: invalidateList,
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o status."),
  });

  function openCreateDialog() {
    setEditingUnit(null);
    form.reset(emptyUnitForm);
    setDialogOpen(true);
  }

  function openEditDialog(unit: UnitSummary) {
    setEditingUnit(unit);
    form.reset({
      name: unit.name,
      address: unit.address ?? "",
      city: unit.city ?? "",
      state: unit.state ?? NO_STATE,
      country: unit.country ?? "Brasil",
    });
    setDialogOpen(true);
  }

  return (
    <div className="flex w-full flex-1 flex-col">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">
          Lojas ou filiais do estabelecimento. Recursos e agendamentos podem ser vinculados a uma unidade.
        </p>
        {isOwner && (
          <Button onClick={openCreateDialog}>
            <Plus className="size-4" />
            Nova unidade
          </Button>
        )}
      </div>

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead>Localização</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Acoes</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {unitsQuery.isLoading ? (
                Array.from({ length: 3 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-48" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-14 rounded-full" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-20" /></TableCell>
                  </TableRow>
                ))
              ) : unitsQuery.data?.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="p-0">
                    <EmptyState
                      icon={Building2}
                      title="Nenhuma unidade cadastrada ainda"
                      description="Cadastre lojas ou filiais do seu estabelecimento."
                      action={
                        isOwner ? (
                          <Button size="sm" onClick={openCreateDialog}>
                            <Plus className="size-4" />
                            Nova unidade
                          </Button>
                        ) : undefined
                      }
                    />
                  </TableCell>
                </TableRow>
              ) : (
                unitsQuery.data?.map((unit) => (
                  <TableRow key={unit.id}>
                    <TableCell className="font-medium">{unit.name}</TableCell>
                    <TableCell>{formatLocation(unit)}</TableCell>
                    <TableCell>
                      <Badge variant={unit.isActive ? "default" : "outline"}>{unit.isActive ? "Ativa" : "Inativa"}</Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      {isOwner ? (
                        <div className="flex justify-end gap-2">
                          <Button variant="ghost" size="sm" onClick={() => openEditDialog(unit)}>
                            Editar
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => statusMutation.mutate({ id: unit.id, isActive: !unit.isActive })}
                          >
                            {unit.isActive ? "Desativar" : "Ativar"}
                          </Button>
                        </div>
                      ) : (
                        <span className="text-muted-foreground text-xs">—</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingUnit ? "Editar unidade" : "Nova unidade"}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form
              onSubmit={form.handleSubmit((values) => (editingUnit ? updateMutation.mutate(values) : createMutation.mutate(values)))}
              className="flex flex-col gap-3"
            >
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nome</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="address"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Endereço</FormLabel>
                    <FormControl>
                      <Input placeholder="Rua, número, bairro" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid grid-cols-2 gap-3">
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
                      <FormLabel>Estado</FormLabel>
                      <Select value={field.value || NO_STATE} onValueChange={field.onChange}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue placeholder="Selecione" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value={NO_STATE}>Não informado</SelectItem>
                          {BRAZILIAN_STATES.map((state) => (
                            <SelectItem key={state.value} value={state.value}>
                              {state.label} ({state.value})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={form.control}
                name="country"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>País</FormLabel>
                    <Select value={field.value || "Brasil"} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecione" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {COUNTRIES.map((country) => (
                          <SelectItem key={country} value={country}>
                            {country}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit" disabled={createMutation.isPending || updateMutation.isPending}>
                  {createMutation.isPending || updateMutation.isPending ? "Salvando..." : "Salvar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
