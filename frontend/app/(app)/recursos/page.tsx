"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowLeft, Clock, Plus, Search, Trash2 } from "lucide-react";

import {
  listResources,
  getResourceById,
  createResource,
  updateResource,
  setResourceActiveStatus,
  setResourceWorkingHours,
  ApiError,
  type ResourceSummary,
  type ResourceType,
  type DayOfWeekName,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const PAGE_SIZE = 20;

const RESOURCE_TYPE_LABELS: Record<ResourceType, string> = {
  Person: "Pessoa",
  Room: "Sala",
  Equipment: "Equipamento",
};

const DAY_OPTIONS: { value: DayOfWeekName; label: string }[] = [
  { value: "Monday", label: "Segunda" },
  { value: "Tuesday", label: "Terca" },
  { value: "Wednesday", label: "Quarta" },
  { value: "Thursday", label: "Quinta" },
  { value: "Friday", label: "Sexta" },
  { value: "Saturday", label: "Sabado" },
  { value: "Sunday", label: "Domingo" },
];

const resourceSchema = z.object({
  name: z.string().min(1, "Informe o nome."),
  type: z.enum(["Person", "Room", "Equipment"]),
  capacity: z.coerce.number().int("Use um numero inteiro.").min(1, "A capacidade precisa ser maior que zero."),
  description: z.string(),
});

// z.coerce faz o tipo de entrada (antes da validacao) divergir do de saida
// (depois da coercao) — o form precisa ser tipado com os dois generics do RHF
// para o resolver aceitar valores brutos de <input> e devolver numeros no submit.
type ResourceFormValues = z.output<typeof resourceSchema>;
type ResourceFormInput = z.input<typeof resourceSchema>;

const emptyResourceForm: ResourceFormInput = { name: "", type: "Person", capacity: 1, description: "" };

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

const workingHoursSchema = z.object({
  entries: z.array(
    z.object({
      dayOfWeek: z.enum(["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]),
      startTime: z.string().min(1, "Informe o horario inicial."),
      endTime: z.string().min(1, "Informe o horario final."),
    })
  ),
});

type WorkingHoursFormValues = z.infer<typeof workingHoursSchema>;

function toTimeInputValue(time: string): string {
  return time.slice(0, 5);
}

function toApiTimeValue(time: string): string {
  return time.length === 5 ? `${time}:00` : time;
}

export default function ResourcesPage() {
  const router = useRouter();
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [page, setPage] = React.useState(1);
  const [searchInput, setSearchInput] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingResource, setEditingResource] = React.useState<ResourceSummary | null>(null);
  const [hoursDialogOpen, setHoursDialogOpen] = React.useState(false);
  const [hoursResource, setHoursResource] = React.useState<ResourceSummary | null>(null);

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const resourcesQuery = useQuery({
    queryKey: ["resources", { page, search }],
    queryFn: () => listResources({ page, pageSize: PAGE_SIZE, search: search || undefined }, accessToken),
    enabled: Boolean(session),
    placeholderData: (previous) => previous,
  });

  const form = useForm<ResourceFormInput, unknown, ResourceFormValues>({
    resolver: zodResolver(resourceSchema),
    defaultValues: emptyResourceForm,
  });

  const hoursForm = useForm<WorkingHoursFormValues>({
    resolver: zodResolver(workingHoursSchema),
    defaultValues: { entries: [] },
  });

  const hoursFieldArray = useFieldArray({ control: hoursForm.control, name: "entries" });

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: ["resources"] });

  const createMutation = useMutation({
    mutationFn: (values: ResourceFormValues) =>
      createResource(
        { name: values.name, type: values.type, capacity: values.capacity, description: toNullable(values.description) },
        accessToken
      ),
    onSuccess: () => {
      toast.success("Recurso cadastrado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cadastrar o recurso."),
  });

  const updateMutation = useMutation({
    mutationFn: (values: ResourceFormValues) => {
      if (!editingResource) {
        throw new Error("Nenhum recurso selecionado.");
      }
      return updateResource(
        editingResource.id,
        { name: values.name, type: values.type, capacity: values.capacity, description: toNullable(values.description) },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Recurso atualizado.");
      invalidateList();
      setDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o recurso."),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setResourceActiveStatus(id, isActive, accessToken),
    onSuccess: invalidateList,
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel atualizar o status."),
  });

  const workingHoursMutation = useMutation({
    mutationFn: (values: WorkingHoursFormValues) => {
      if (!hoursResource) {
        throw new Error("Nenhum recurso selecionado.");
      }
      return setResourceWorkingHours(
        hoursResource.id,
        values.entries.map((entry) => ({
          dayOfWeek: entry.dayOfWeek,
          startTime: toApiTimeValue(entry.startTime),
          endTime: toApiTimeValue(entry.endTime),
        })),
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Horarios atualizados.");
      setHoursDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel salvar os horarios."),
  });

  const totalPages = Math.max(1, Math.ceil((resourcesQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  function openCreateDialog() {
    setEditingResource(null);
    form.reset(emptyResourceForm);
    setDialogOpen(true);
  }

  async function openEditDialog(resource: ResourceSummary) {
    try {
      const details = await getResourceById(resource.id, accessToken);
      setEditingResource(resource);
      form.reset({
        name: details.name,
        type: details.type,
        capacity: details.capacity,
        description: details.description ?? "",
      });
      setDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar o recurso.");
    }
  }

  async function openHoursDialog(resource: ResourceSummary) {
    try {
      const details = await getResourceById(resource.id, accessToken);
      setHoursResource(resource);
      hoursForm.reset({
        entries: details.workingHours.map((entry) => ({
          dayOfWeek: entry.dayOfWeek,
          startTime: toTimeInputValue(entry.startTime),
          endTime: toTimeInputValue(entry.endTime),
        })),
      });
      setHoursDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar os horarios.");
    }
  }

  if (!session) {
    return null;
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-4xl flex-1 flex-col p-6 sm:p-10">
      <Link href="/painel" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Recursos</h1>
          <p className="text-muted-foreground mt-1 text-sm">Pessoas, salas e equipamentos que a agenda reserva.</p>
        </div>
        <Button onClick={openCreateDialog}>
          <Plus className="size-4" />
          Novo recurso
        </Button>
      </div>

      <div className="mb-4 flex gap-2">
        <Input
          placeholder="Buscar por nome..."
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              setPage(1);
              setSearch(searchInput);
            }
          }}
        />
        <Button
          variant="outline"
          onClick={() => {
            setPage(1);
            setSearch(searchInput);
          }}
        >
          <Search className="size-4" />
        </Button>
      </div>

      <div className="rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Nome</TableHead>
              <TableHead>Tipo</TableHead>
              <TableHead>Capacidade</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Acoes</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {resourcesQuery.data?.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground py-6 text-center">
                  Nenhum recurso encontrado.
                </TableCell>
              </TableRow>
            )}
            {resourcesQuery.data?.items.map((resource) => (
              <TableRow key={resource.id}>
                <TableCell className="font-medium">{resource.name}</TableCell>
                <TableCell>{RESOURCE_TYPE_LABELS[resource.type]}</TableCell>
                <TableCell>{resource.capacity}</TableCell>
                <TableCell>
                  <Badge variant={resource.isActive ? "default" : "outline"}>
                    {resource.isActive ? "Ativo" : "Inativo"}
                  </Badge>
                </TableCell>
                <TableCell className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => openHoursDialog(resource)}>
                      <Clock className="size-4" />
                      Horarios
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => openEditDialog(resource)}>
                      Editar
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => statusMutation.mutate({ id: resource.id, isActive: !resource.isActive })}
                    >
                      {resource.isActive ? "Desativar" : "Ativar"}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="mt-4 flex items-center justify-between text-sm">
        <span className="text-muted-foreground">
          Pagina {resourcesQuery.data?.page ?? page} de {totalPages}
        </span>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
            Anterior
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
            Proxima
          </Button>
        </div>
      </div>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingResource ? "Editar recurso" : "Novo recurso"}</DialogTitle>
          </DialogHeader>
          <Form {...form}>
            <form
              onSubmit={form.handleSubmit((values) =>
                editingResource ? updateMutation.mutate(values) : createMutation.mutate(values)
              )}
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
              <div className="grid grid-cols-2 gap-3">
                <FormField
                  control={form.control}
                  name="type"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Tipo</FormLabel>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {Object.entries(RESOURCE_TYPE_LABELS).map(([value, label]) => (
                            <SelectItem key={value} value={value}>
                              {label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="capacity"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Capacidade</FormLabel>
                      <FormControl>
                        <Input type="number" min={1} {...field} value={field.value as number} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <FormField
                control={form.control}
                name="description"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Descricao</FormLabel>
                    <FormControl>
                      <Textarea rows={3} {...field} />
                    </FormControl>
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

      <Dialog open={hoursDialogOpen} onOpenChange={setHoursDialogOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Horarios de {hoursResource?.name}</DialogTitle>
          </DialogHeader>
          <Form {...hoursForm}>
            <form
              onSubmit={hoursForm.handleSubmit((values) => workingHoursMutation.mutate(values))}
              className="flex flex-col gap-3"
            >
              {hoursFieldArray.fields.length === 0 && (
                <p className="text-muted-foreground text-sm">Nenhum horario cadastrado ainda.</p>
              )}
              {hoursFieldArray.fields.map((field, index) => (
                <div key={field.id} className="flex items-end gap-2">
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.dayOfWeek`}
                    render={({ field: dayField }) => (
                      <FormItem className="flex-1">
                        {index === 0 && <FormLabel>Dia</FormLabel>}
                        <Select value={dayField.value} onValueChange={dayField.onChange}>
                          <FormControl>
                            <SelectTrigger className="w-full">
                              <SelectValue />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            {DAY_OPTIONS.map((day) => (
                              <SelectItem key={day.value} value={day.value}>
                                {day.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.startTime`}
                    render={({ field: startField }) => (
                      <FormItem>
                        {index === 0 && <FormLabel>Inicio</FormLabel>}
                        <FormControl>
                          <Input type="time" {...startField} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={hoursForm.control}
                    name={`entries.${index}.endTime`}
                    render={({ field: endField }) => (
                      <FormItem>
                        {index === 0 && <FormLabel>Fim</FormLabel>}
                        <FormControl>
                          <Input type="time" {...endField} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label="Remover horario"
                    onClick={() => hoursFieldArray.remove(index)}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              ))}
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="self-start"
                onClick={() => hoursFieldArray.append({ dayOfWeek: "Monday", startTime: "09:00", endTime: "18:00" })}
              >
                <Plus className="size-4" />
                Adicionar horario
              </Button>
              <DialogFooter>
                <Button type="submit" disabled={workingHoursMutation.isPending}>
                  {workingHoursMutation.isPending ? "Salvando..." : "Salvar horarios"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </main>
  );
}
