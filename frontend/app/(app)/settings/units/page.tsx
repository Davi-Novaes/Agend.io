"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Building2, CheckCircle2, CircleOff, Clock3, MapPin, MessageCircle, MoreHorizontal, Pencil, Phone, Plus, RefreshCw, Search, Store, Trash2 } from "lucide-react";

import { listUnits, createUnit, updateUnit, setUnitActiveStatus, setUnitBusinessHours, ApiError, type DayOfWeekName, type UnitSummary } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { decodeJwtRole } from "@/lib/auth/decode-jwt";
import { BRAZILIAN_STATES, COUNTRIES } from "@/lib/brazilian-states";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

const NO_STATE = "__none__";

const unitSchema = z.object({
  name: z.string().trim().min(1, "Informe o nome.").max(200, "Use no máximo 200 caracteres."),
  address: z.string().max(500, "Use no máximo 500 caracteres."),
  city: z.string().max(150, "Use no máximo 150 caracteres."),
  state: z.string(),
  country: z.string(),
  phone: z.string().max(30, "Use no máximo 30 caracteres."),
  whatsApp: z.string().max(30, "Use no máximo 30 caracteres."),
});

type UnitFormValues = z.infer<typeof unitSchema>;
const emptyUnitForm: UnitFormValues = { name: "", address: "", city: "", state: "", country: "Brasil", phone: "", whatsApp: "" };

const DAY_OPTIONS: { value: DayOfWeekName; label: string }[] = [
  { value: "Monday", label: "Segunda" }, { value: "Tuesday", label: "Terça" }, { value: "Wednesday", label: "Quarta" },
  { value: "Thursday", label: "Quinta" }, { value: "Friday", label: "Sexta" }, { value: "Saturday", label: "Sábado" }, { value: "Sunday", label: "Domingo" },
];

const hoursSchema = z.object({
  entries: z.array(z.object({
    dayOfWeek: z.enum(["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]),
    startTime: z.string().min(1, "Informe o início."),
    endTime: z.string().min(1, "Informe o fim."),
  })).refine((entries) => entries.every((entry) => entry.endTime > entry.startTime), { message: "O horário final deve ser posterior ao inicial." }),
});
type HoursFormValues = z.infer<typeof hoursSchema>;

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

function formatLocation(unit: UnitSummary): string {
  const cityState = [unit.city, unit.state].filter(Boolean).join(" / ");
  return [unit.address, cityState].filter(Boolean).join(" — ") || "—";
}

function formatRegion(unit: UnitSummary): string {
  return [unit.city, unit.state, unit.country].filter(Boolean).join(" · ") || "Cidade e estado não informados";
}

function unitInitials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase();
}

type StatusFilter = "all" | "active" | "inactive";

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
  const [hoursDialogUnit, setHoursDialogUnit] = React.useState<UnitSummary | null>(null);
  const [search, setSearch] = React.useState("");
  const [statusFilter, setStatusFilter] = React.useState<StatusFilter>("all");

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
  const hoursForm = useForm<HoursFormValues>({ resolver: zodResolver(hoursSchema), defaultValues: { entries: [] } });
  const hoursFields = useFieldArray({ control: hoursForm.control, name: "entries" });

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: ["units"] });

  const units = React.useMemo(() => unitsQuery.data ?? [], [unitsQuery.data]);
  const activeCount = units.filter((unit) => unit.isActive).length;
  const inactiveCount = units.length - activeCount;
  const locatedCount = units.filter((unit) => unit.address || unit.city || unit.state).length;
  const filteredUnits = React.useMemo(() => {
    const normalizedSearch = search.trim().toLocaleLowerCase("pt-BR");
    return units.filter((unit) => {
      const matchesStatus = statusFilter === "all" || (statusFilter === "active" ? unit.isActive : !unit.isActive);
      const searchable = [unit.name, unit.address, unit.city, unit.state, unit.country].filter(Boolean).join(" ").toLocaleLowerCase("pt-BR");
      return matchesStatus && (!normalizedSearch || searchable.includes(normalizedSearch));
    });
  }, [search, statusFilter, units]);

  const createMutation = useMutation({
    mutationFn: (values: UnitFormValues) =>
      createUnit(
        {
          name: values.name,
          address: toNullable(values.address),
          city: toNullable(values.city),
          state: values.state === NO_STATE ? null : toNullable(values.state),
          country: toNullable(values.country),
          phone: toNullable(values.phone),
          whatsApp: toNullable(values.whatsApp),
        },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Local cadastrado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível cadastrar o local."),
  });

  const updateMutation = useMutation({
    mutationFn: (values: UnitFormValues) => {
      if (!editingUnit) {
        throw new Error("Nenhum local selecionado.");
      }
      return updateUnit(
        editingUnit.id,
        {
          name: values.name,
          address: toNullable(values.address),
          city: toNullable(values.city),
          state: values.state === NO_STATE ? null : toNullable(values.state),
          country: toNullable(values.country),
          phone: toNullable(values.phone),
          whatsApp: toNullable(values.whatsApp),
        },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Local atualizado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível atualizar o local."),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setUnitActiveStatus(id, isActive, accessToken),
    onSuccess: (_, variables) => {
      toast.success(variables.isActive ? "Local ativado." : "Local desativado.");
      invalidateList();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível atualizar o status."),
  });

  const hoursMutation = useMutation({
    mutationFn: (values: HoursFormValues) => {
      if (!hoursDialogUnit) throw new Error("Nenhum local selecionado.");
      return setUnitBusinessHours(hoursDialogUnit.id, values.entries.map((entry) => ({
        ...entry,
        startTime: entry.startTime.length === 5 ? `${entry.startTime}:00` : entry.startTime,
        endTime: entry.endTime.length === 5 ? `${entry.endTime}:00` : entry.endTime,
      })), accessToken);
    },
    onSuccess: () => {
      toast.success("Horários do local atualizados.");
      invalidateList();
      setHoursDialogUnit(null);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível atualizar os horários."),
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
      phone: unit.phone ?? "",
      whatsApp: unit.whatsApp ?? "",
    });
    setDialogOpen(true);
  }

  function openHoursDialog(unit: UnitSummary) {
    setHoursDialogUnit(unit);
    hoursForm.reset({ entries: (unit.businessHours ?? []).map((entry) => ({
      dayOfWeek: entry.dayOfWeek,
      startTime: entry.startTime.slice(0, 5),
      endTime: entry.endTime.slice(0, 5),
    })) });
  }

  return (
    <div className="flex w-full flex-1 flex-col gap-6">
      <section className="relative overflow-hidden rounded-2xl border bg-gradient-to-br from-primary/12 via-card to-card p-5 sm:p-6">
        <div aria-hidden className="absolute -right-10 -top-16 size-52 rounded-full bg-primary/10 blur-3xl" />
        <div className="relative flex flex-col justify-between gap-5 sm:flex-row sm:items-center">
          <div className="flex items-start gap-4">
            <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-lg shadow-primary/20"><Building2 className="size-5" /></span>
            <div>
              <p className="text-xs font-semibold tracking-[0.18em] text-primary uppercase">Estrutura do negócio</p>
              <h2 className="mt-1 text-xl font-semibold tracking-tight">Gerencie seus locais de atendimento</h2>
              <p className="mt-1 max-w-2xl text-sm leading-relaxed text-muted-foreground">Organize lojas e filiais para vincular profissionais, agenda e atendimentos ao local correto.</p>
            </div>
          </div>
          {isOwner && <Button onClick={openCreateDialog} className="shrink-0"><Plus className="size-4" />Novo local</Button>}
        </div>
      </section>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label="Resumo das unidades">
        {[
          { label: "Total de locais", value: units.length, icon: Store, color: "text-primary bg-primary/10" },
          { label: "Em operação", value: activeCount, icon: CheckCircle2, color: "text-emerald-600 bg-emerald-500/10" },
          { label: "Inativas", value: inactiveCount, icon: CircleOff, color: "text-amber-600 bg-amber-500/10" },
          { label: "Com endereço", value: locatedCount, icon: MapPin, color: "text-sky-600 bg-sky-500/10" },
        ].map((metric) => {
          const Icon = metric.icon;
          return <Card key={metric.label}><CardContent className="flex items-center gap-3 p-4"><span className={`flex size-10 items-center justify-center rounded-xl ${metric.color}`}><Icon className="size-5" /></span><div><p className="text-2xl font-semibold tracking-tight">{unitsQuery.isLoading ? "—" : metric.value}</p><p className="text-xs text-muted-foreground">{metric.label}</p></div></CardContent></Card>;
        })}
      </section>

      <section className="flex flex-col gap-4">
        <div className="flex flex-col justify-between gap-3 lg:flex-row lg:items-center">
          <div><h3 className="font-semibold">Locais cadastrados</h3><p className="text-sm text-muted-foreground">Edite os dados ou pause temporariamente uma operação.</p></div>
          <div className="flex flex-col gap-2 sm:flex-row">
            <div className="relative min-w-0 sm:w-64"><Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar local ou cidade" className="pl-9" aria-label="Buscar locais" /></div>
            <div className="flex rounded-lg border bg-card p-1" aria-label="Filtrar unidades por status">
              {(["all", "active", "inactive"] as const).map((value) => <Button key={value} type="button" variant={statusFilter === value ? "secondary" : "ghost"} size="sm" className="h-7 px-3" onClick={() => setStatusFilter(value)}>{value === "all" ? "Todas" : value === "active" ? "Ativas" : "Inativas"}</Button>)}
            </div>
          </div>
        </div>

        {unitsQuery.isLoading ? (
          <div className="grid gap-3 md:grid-cols-2">{Array.from({ length: 2 }).map((_, index) => <Skeleton key={index} className="h-44 rounded-xl" />)}</div>
        ) : unitsQuery.isError ? (
          <Card><CardContent><EmptyState icon={Building2} title="Não foi possível carregar os locais" description="Verifique sua conexão e tente novamente." action={<Button size="sm" variant="outline" onClick={() => unitsQuery.refetch()}><RefreshCw className="size-4" />Tentar novamente</Button>} /></CardContent></Card>
        ) : units.length === 0 ? (
          <Card><CardContent><EmptyState icon={Building2} title="Cadastre seu primeiro local" description="Crie o local principal do seu negócio. Depois você poderá vinculá-lo aos profissionais e agendamentos." action={isOwner ? <Button size="sm" onClick={openCreateDialog}><Plus className="size-4" />Criar primeiro local</Button> : undefined} /></CardContent></Card>
        ) : filteredUnits.length === 0 ? (
          <Card><CardContent><EmptyState icon={Search} title="Nenhum local encontrado" description="Tente outro nome, cidade ou filtro de status." action={<Button size="sm" variant="outline" onClick={() => { setSearch(""); setStatusFilter("all"); }}>Limpar filtros</Button>} /></CardContent></Card>
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            {filteredUnits.map((unit) => {
              const statusPending = statusMutation.isPending && statusMutation.variables?.id === unit.id;
              return (
                <Card key={unit.id} className="group overflow-hidden transition-all hover:border-primary/30 hover:shadow-md">
                  <CardContent className="flex h-full flex-col p-0">
                    <div className="flex items-start justify-between gap-3 p-5">
                      <div className="flex min-w-0 items-center gap-3">
                        <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-sm font-semibold text-primary">{unitInitials(unit.name)}</span>
                        <div className="min-w-0"><p className="truncate font-semibold">{unit.name}</p><div className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground"><MapPin className="size-3.5 shrink-0" /><span className="truncate">{formatRegion(unit)}</span></div></div>
                      </div>
                      <Badge variant={unit.isActive ? "success" : "secondary"}>{unit.isActive ? "Em operação" : "Inativa"}</Badge>
                    </div>
                    <div className="flex-1 space-y-3 border-y bg-muted/15 px-5 py-3"><div><p className="text-xs font-medium text-muted-foreground">Endereço</p><p className="mt-1 text-sm">{formatLocation(unit) === "—" ? "Endereço ainda não preenchido" : formatLocation(unit)}</p></div>{(unit.phone || unit.whatsApp) && <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">{unit.phone && <span className="flex items-center gap-1.5"><Phone className="size-3.5" />{unit.phone}</span>}{unit.whatsApp && <span className="flex items-center gap-1.5"><MessageCircle className="size-3.5" />{unit.whatsApp}</span>}</div>}</div>
                    <div className="flex items-center justify-between gap-2 p-3 pl-5">
                      <p className="text-xs text-muted-foreground">{unit.isActive ? "Disponível para vínculos e agendamentos" : "Não disponível para novos vínculos"}</p>
                      {isOwner && <div className="flex items-center gap-1"><Button type="button" variant="ghost" size="sm" onClick={() => openHoursDialog(unit)}><Clock3 className="size-3.5" />Horários</Button><Button type="button" variant="ghost" size="sm" onClick={() => openEditDialog(unit)}><Pencil className="size-3.5" />Editar</Button><DropdownMenu><DropdownMenuTrigger asChild><Button variant="ghost" size="icon" aria-label={`Mais ações de ${unit.name}`} disabled={statusPending}><MoreHorizontal className="size-4" /></Button></DropdownMenuTrigger><DropdownMenuContent align="end" className="w-44"><DropdownMenuItem onSelect={() => openEditDialog(unit)}>Editar informações</DropdownMenuItem><DropdownMenuItem onSelect={() => openHoursDialog(unit)}>Editar horários</DropdownMenuItem><DropdownMenuItem variant={unit.isActive ? "destructive" : "default"} onSelect={() => statusMutation.mutate({ id: unit.id, isActive: !unit.isActive })}>{unit.isActive ? "Desativar local" : "Ativar local"}</DropdownMenuItem></DropdownMenuContent></DropdownMenu></div>}
                    </div>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        )}
      </section>

      <Dialog open={dialogOpen} onOpenChange={(open) => { setDialogOpen(open); if (!open) setEditingUnit(null); }}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <div className="flex items-start gap-3"><span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Building2 className="size-5" /></span><div><DialogTitle>{editingUnit ? "Editar local" : "Novo local"}</DialogTitle><DialogDescription className="mt-1">{editingUnit ? "Atualize o nome, contato e endereço deste local." : "Cadastre uma loja, filial ou local de atendimento."}</DialogDescription></div></div>
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
                      <Input placeholder="Ex.: Loja Centro" autoFocus {...field} />
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
              <div className="grid gap-3 sm:grid-cols-2">
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
              <div className="grid gap-3 sm:grid-cols-2">
                <FormField control={form.control} name="phone" render={({ field }) => <FormItem><FormLabel>Telefone</FormLabel><FormControl><Input placeholder="(11) 3333-4444" autoComplete="tel" {...field} /></FormControl><FormMessage /></FormItem>} />
                <FormField control={form.control} name="whatsApp" render={({ field }) => <FormItem><FormLabel>WhatsApp</FormLabel><FormControl><Input placeholder="(11) 99999-8888" autoComplete="tel" {...field} /></FormControl><FormMessage /></FormItem>} />
              </div>
              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>Cancelar</Button>
                <Button type="submit" disabled={createMutation.isPending || updateMutation.isPending}>
                  {createMutation.isPending || updateMutation.isPending ? "Salvando..." : editingUnit ? "Salvar alterações" : "Cadastrar local"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <Dialog open={hoursDialogUnit !== null} onOpenChange={(open) => { if (!open) setHoursDialogUnit(null); }}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <div className="flex items-start gap-3"><span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Clock3 className="size-5" /></span><div><DialogTitle>Horários de {hoursDialogUnit?.name}</DialogTitle><DialogDescription className="mt-1">Se não houver horários próprios, o local seguirá o horário geral configurado em Marca.</DialogDescription></div></div>
          </DialogHeader>
          <Form {...hoursForm}>
            <form onSubmit={hoursForm.handleSubmit((values) => hoursMutation.mutate(values))} className="flex flex-col gap-3">
              {hoursFields.fields.length === 0 && <div className="rounded-xl border border-dashed bg-muted/20 p-5 text-center"><p className="text-sm font-medium">Usando o horário geral</p><p className="mt-1 text-xs text-muted-foreground">Adicione horários somente se este local funcionar em períodos diferentes.</p></div>}
              {hoursFields.fields.map((field, index) => (
                <div key={field.id} className="grid items-end gap-2 rounded-xl border bg-muted/15 p-3 sm:grid-cols-[minmax(0,1fr)_120px_120px_auto]">
                  <FormField control={hoursForm.control} name={`entries.${index}.dayOfWeek`} render={({ field: dayField }) => <FormItem><FormLabel className={index === 0 ? undefined : "sr-only"}>Dia</FormLabel><Select value={dayField.value} onValueChange={dayField.onChange}><FormControl><SelectTrigger className="w-full"><SelectValue /></SelectTrigger></FormControl><SelectContent>{DAY_OPTIONS.map((day) => <SelectItem key={day.value} value={day.value}>{day.label}</SelectItem>)}</SelectContent></Select></FormItem>} />
                  <FormField control={hoursForm.control} name={`entries.${index}.startTime`} render={({ field: startField }) => <FormItem><FormLabel className={index === 0 ? undefined : "sr-only"}>Início</FormLabel><FormControl><Input type="time" {...startField} /></FormControl></FormItem>} />
                  <FormField control={hoursForm.control} name={`entries.${index}.endTime`} render={({ field: endField }) => <FormItem><FormLabel className={index === 0 ? undefined : "sr-only"}>Fim</FormLabel><FormControl><Input type="time" {...endField} /></FormControl></FormItem>} />
                  <Button type="button" variant="ghost" size="icon" aria-label="Remover horário" onClick={() => hoursFields.remove(index)}><Trash2 className="size-4" /></Button>
                </div>
              ))}
              {hoursForm.formState.errors.entries?.message && <p role="alert" className="text-sm text-destructive">{hoursForm.formState.errors.entries.message}</p>}
              <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => hoursFields.append({ dayOfWeek: "Monday", startTime: "09:00", endTime: "18:00" })}><Plus className="size-4" />Adicionar horário</Button>
              <DialogFooter className="mt-2"><Button type="button" variant="outline" onClick={() => setHoursDialogUnit(null)}>Cancelar</Button><Button type="submit" disabled={hoursMutation.isPending}>{hoursMutation.isPending ? "Salvando..." : "Salvar horários"}</Button></DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
