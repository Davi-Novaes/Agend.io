"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Armchair, Briefcase, CalendarOff, Clock, Plus, Search, Trash2 } from "lucide-react";

import {
  listResources,
  getResourceById,
  getTenantProfile,
  createResource,
  updateResource,
  setResourceActiveStatus,
  setResourceWorkingHours,
  uploadResourcePhoto,
  setResourceSpecialties,
  setResourceServices,
  listTimeOffs,
  createTimeOff,
  deleteTimeOff,
  listUnits,
  listServices,
  resolveAssetUrl,
  ApiError,
  type ResourceSummary,
  type ResourceType,
  type DayOfWeekName,
  type TimeOffSummary,
  type WorkingHourEntry,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
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

// Radix Select nao aceita value="" num item — usa um sentinela pra "sem unidade".
const NO_UNIT_VALUE = "none";

const resourceSchema = z.object({
  name: z.string().min(1, "Informe o nome."),
  type: z.enum(["Person", "Room", "Equipment"]),
  capacity: z.coerce.number().int("Use um numero inteiro.").min(1, "A capacidade precisa ser maior que zero."),
  description: z.string(),
  unitId: z.string(),
});

// z.coerce faz o tipo de entrada (antes da validacao) divergir do de saida
// (depois da coercao) — o form precisa ser tipado com os dois generics do RHF
// para o resolver aceitar valores brutos de <input> e devolver numeros no submit.
type ResourceFormValues = z.output<typeof resourceSchema>;
type ResourceFormInput = z.input<typeof resourceSchema>;

const emptyResourceForm: ResourceFormInput = { name: "", type: "Person", capacity: 1, description: "", unitId: NO_UNIT_VALUE };

function toNullable(value: string): string | null {
  return value.trim() === "" ? null : value.trim();
}

const ALLOWED_PHOTO_TYPES = ["image/png", "image/jpeg", "image/webp"];
const MAX_PHOTO_SIZE_BYTES = 2 * 1024 * 1024;

function parseSpecialties(value: string): string[] {
  return value
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
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

// Sem isso, um recurso novo nao tem NENHUM horario de trabalho e a pagina
// publica de agendamento mostra "nenhum horario disponivel" pra sempre, ate o
// dono descobrir a tela separada de "Horarios" — trava silenciosa real,
// confirmada ao vivo pela Persona A da auditoria (BL-09, docs/BACKLOG.md).
// Segunda a sabado, comercial, editavel a qualquer momento no mesmo dialog
// que ja existe — nao inventa uma feature nova, so preenche o que ja tinha
// que ser preenchido manualmente.
const DEFAULT_WORKING_HOURS: WorkingHourEntry[] = (
  ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"] as const
).map((dayOfWeek) => ({ dayOfWeek, startTime: "09:00:00", endTime: "18:00:00" }));

export default function ResourcesPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [page, setPage] = React.useState(1);
  const [searchInput, setSearchInput] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingResource, setEditingResource] = React.useState<ResourceSummary | null>(null);
  const [hoursDialogOpen, setHoursDialogOpen] = React.useState(false);
  const [hoursResource, setHoursResource] = React.useState<ResourceSummary | null>(null);
  const [photoPreviewUrl, setPhotoPreviewUrl] = React.useState<string | null>(null);
  const [selectedPhotoFile, setSelectedPhotoFile] = React.useState<File | null>(null);
  const [isUploadingPhoto, setIsUploadingPhoto] = React.useState(false);
  const [specialtiesInput, setSpecialtiesInput] = React.useState("");
  const photoInputRef = React.useRef<HTMLInputElement>(null);
  const [servicesDialogOpen, setServicesDialogOpen] = React.useState(false);
  const [servicesResource, setServicesResource] = React.useState<ResourceSummary | null>(null);
  const [selectedServiceIds, setSelectedServiceIds] = React.useState<string[]>([]);
  const [timeOffDialogOpen, setTimeOffDialogOpen] = React.useState(false);
  const [timeOffResource, setTimeOffResource] = React.useState<ResourceSummary | null>(null);
  const [newTimeOffStart, setNewTimeOffStart] = React.useState("");
  const [newTimeOffEnd, setNewTimeOffEnd] = React.useState("");
  const [newTimeOffReason, setNewTimeOffReason] = React.useState("");

  const accessToken = session?.accessToken ?? "";

  const resourcesQuery = useQuery({
    queryKey: ["resources", { page, search }],
    queryFn: () => listResources({ page, pageSize: PAGE_SIZE, search: search || undefined }, accessToken),
    enabled: Boolean(session),
    placeholderData: (previous) => previous,
  });

  // Onboarding promete que "Profissional" vira o termo do segmento (ex.
  // "Barbeiro" numa barbearia) — sem isso a pagina ficava rotulada
  // genericamente "Recursos" mesmo quando o resto do produto ja fala a
  // lingua do segmento (BL-14, docs/BACKLOG.md).
  const profileQuery = useQuery({
    queryKey: ["tenant-profile"],
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(session),
  });
  const staffLabel = profileQuery.data?.terminology.staff ?? "Recurso";

  const unitsQuery = useQuery({
    queryKey: ["units"],
    queryFn: () => listUnits(accessToken),
    enabled: Boolean(session),
  });

  const allServicesQuery = useQuery({
    queryKey: ["services", "all-for-resource-link"],
    queryFn: () => listServices({ pageSize: 100 }, accessToken),
    enabled: Boolean(session) && servicesDialogOpen,
  });

  const timeOffsQuery = useQuery({
    queryKey: ["time-off", timeOffResource?.id],
    queryFn: () => listTimeOffs(timeOffResource!.id, accessToken),
    enabled: Boolean(session) && timeOffDialogOpen && Boolean(timeOffResource),
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
        {
          name: values.name,
          type: values.type,
          capacity: values.capacity,
          description: toNullable(values.description),
          unitId: values.unitId === NO_UNIT_VALUE ? null : values.unitId,
        },
        accessToken
      ),
    onSuccess: async (result) => {
      // Falha ao pre-preencher o horario nao pode travar o cadastro em si —
      // o recurso ja foi criado com sucesso; o dono sempre pode configurar
      // manualmente depois em "Horarios" se isso aqui nao completar.
      try {
        await setResourceWorkingHours(result.id, DEFAULT_WORKING_HOURS, accessToken);
      } catch {
        toast.error("Recurso cadastrado, mas nao foi possivel pre-preencher o horario padrao. Configure em \"Horarios\".");
      }
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
        {
          name: values.name,
          type: values.type,
          capacity: values.capacity,
          description: toNullable(values.description),
          unitId: values.unitId === NO_UNIT_VALUE ? null : values.unitId,
        },
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

  const specialtiesMutation = useMutation({
    mutationFn: (specialties: string[]) => {
      if (!editingResource) {
        throw new Error("Nenhum recurso selecionado.");
      }
      return setResourceSpecialties(editingResource.id, specialties, accessToken);
    },
    onSuccess: () => {
      toast.success("Especialidades atualizadas.");
      invalidateList();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel salvar as especialidades."),
  });

  const servicesMutation = useMutation({
    mutationFn: (serviceIds: string[]) => {
      if (!servicesResource) {
        throw new Error("Nenhum recurso selecionado.");
      }
      return setResourceServices(servicesResource.id, serviceIds, accessToken);
    },
    onSuccess: () => {
      toast.success("Servicos vinculados atualizados.");
      setServicesDialogOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel salvar os servicos vinculados."),
  });

  const createTimeOffMutation = useMutation({
    mutationFn: (input: { startDate: string; endDate: string; reason: string | null }) => {
      if (!timeOffResource) {
        throw new Error("Nenhum recurso selecionado.");
      }
      return createTimeOff(timeOffResource.id, input, accessToken);
    },
    onSuccess: () => {
      toast.success("Folga cadastrada.");
      setNewTimeOffStart("");
      setNewTimeOffEnd("");
      setNewTimeOffReason("");
      queryClient.invalidateQueries({ queryKey: ["time-off", timeOffResource?.id] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cadastrar a folga."),
  });

  const deleteTimeOffMutation = useMutation({
    mutationFn: (timeOffId: string) => deleteTimeOff(timeOffId, accessToken),
    onSuccess: () => {
      toast.success("Folga removida.");
      queryClient.invalidateQueries({ queryKey: ["time-off", timeOffResource?.id] });
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel remover a folga."),
  });

  const totalPages = Math.max(1, Math.ceil((resourcesQuery.data?.totalCount ?? 0) / PAGE_SIZE));

  function openCreateDialog() {
    setEditingResource(null);
    form.reset(emptyResourceForm);
    setPhotoPreviewUrl(null);
    setSelectedPhotoFile(null);
    setSpecialtiesInput("");
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
        unitId: details.unitId ?? NO_UNIT_VALUE,
      });
      setPhotoPreviewUrl(details.photoUrl ? resolveAssetUrl(details.photoUrl) : null);
      setSelectedPhotoFile(null);
      setSpecialtiesInput(details.specialties.join(", "));
      setDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar o recurso.");
    }
  }

  function handlePhotoFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    if (!ALLOWED_PHOTO_TYPES.includes(file.type)) {
      toast.error("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
      return;
    }

    if (file.size > MAX_PHOTO_SIZE_BYTES) {
      toast.error("O arquivo nao pode ter mais que 2MB.");
      return;
    }

    setSelectedPhotoFile(file);
    setPhotoPreviewUrl(URL.createObjectURL(file));
  }

  async function handlePhotoUpload() {
    if (!selectedPhotoFile || !editingResource) {
      return;
    }

    setIsUploadingPhoto(true);
    try {
      const result = await uploadResourcePhoto(editingResource.id, selectedPhotoFile, accessToken);
      setPhotoPreviewUrl(resolveAssetUrl(result.photoUrl));
      setSelectedPhotoFile(null);
      invalidateList();
      toast.success("Foto atualizada.");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel enviar a foto.");
    } finally {
      setIsUploadingPhoto(false);
    }
  }

  async function openServicesDialog(resource: ResourceSummary) {
    try {
      const details = await getResourceById(resource.id, accessToken);
      setServicesResource(resource);
      setSelectedServiceIds(details.serviceIds);
      setServicesDialogOpen(true);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Nao foi possivel carregar os servicos do recurso.");
    }
  }

  function toggleServiceId(serviceId: string) {
    setSelectedServiceIds((current) =>
      current.includes(serviceId) ? current.filter((id) => id !== serviceId) : [...current, serviceId]
    );
  }

  function openTimeOffDialog(resource: ResourceSummary) {
    setTimeOffResource(resource);
    setNewTimeOffStart("");
    setNewTimeOffEnd("");
    setNewTimeOffReason("");
    setTimeOffDialogOpen(true);
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

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-1 flex-col">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">Pessoas, salas e equipamentos que a agenda reserva.</p>
        <Button onClick={openCreateDialog}>
          <Plus className="size-4" />
          Novo {staffLabel}
        </Button>
      </div>

      <Card>
        <CardContent className="flex flex-col gap-4">
          <div className="flex gap-2">
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
              size="icon"
              aria-label="Buscar recursos"
              onClick={() => {
                setPage(1);
                setSearch(searchInput);
              }}
            >
              <Search className="size-4" />
            </Button>
          </div>

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
              {resourcesQuery.isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-14 rounded-full" /></TableCell>
                    <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-32" /></TableCell>
                  </TableRow>
                ))
              ) : resourcesQuery.data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="p-0">
                    {search ? (
                      <EmptyState
                        icon={Search}
                        title={`Nenhum ${staffLabel.toLowerCase()} encontrado para "${search}"`}
                        description="Tente ajustar os termos da busca ou limpe o filtro."
                        action={
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setSearchInput("");
                              setSearch("");
                              setPage(1);
                            }}
                          >
                            Limpar busca
                          </Button>
                        }
                      />
                    ) : (
                      <EmptyState
                        icon={Armchair}
                        title={`Nenhum ${staffLabel.toLowerCase()} cadastrado ainda`}
                        description="Cadastre pessoas, salas ou equipamentos que a agenda vai reservar."
                        action={
                          <Button size="sm" onClick={openCreateDialog}>
                            <Plus className="size-4" />
                            Novo {staffLabel}
                          </Button>
                        }
                      />
                    )}
                  </TableCell>
                </TableRow>
              ) : (
                resourcesQuery.data?.items.map((resource) => (
                  <TableRow key={resource.id}>
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        {resource.photoUrl ? (
                          // eslint-disable-next-line @next/next/no-img-element -- miniatura de URL dinamica da API, nao um asset estatico do build.
                          <img
                            src={resolveAssetUrl(resource.photoUrl)}
                            alt=""
                            className="bg-muted size-8 shrink-0 rounded-full object-cover"
                          />
                        ) : (
                          <div className="bg-muted flex size-8 shrink-0 items-center justify-center rounded-full">
                            <Armchair className="text-muted-foreground size-4" />
                          </div>
                        )}
                        <div>
                          {resource.name}
                          {resource.specialties.length > 0 && (
                            <p className="text-muted-foreground text-xs">{resource.specialties.join(", ")}</p>
                          )}
                        </div>
                      </div>
                    </TableCell>
                    <TableCell>{RESOURCE_TYPE_LABELS[resource.type]}</TableCell>
                    <TableCell>{resource.capacity}</TableCell>
                    <TableCell>
                      <Badge variant={resource.isActive ? "default" : "outline"}>
                        {resource.isActive ? "Ativo" : "Inativo"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex flex-wrap justify-end gap-2">
                        <Button variant="ghost" size="sm" onClick={() => openHoursDialog(resource)}>
                          <Clock className="size-4" />
                          Horarios
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => openServicesDialog(resource)}>
                          <Briefcase className="size-4" />
                          Servicos
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => openTimeOffDialog(resource)}>
                          <CalendarOff className="size-4" />
                          Folgas
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
                ))
              )}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground" aria-live="polite">
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
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingResource ? `Editar ${staffLabel}` : `Novo ${staffLabel}`}</DialogTitle>
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
              {unitsQuery.data && unitsQuery.data.length > 0 && (
                <FormField
                  control={form.control}
                  name="unitId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Unidade</FormLabel>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value={NO_UNIT_VALUE}>Nenhuma</SelectItem>
                          {unitsQuery.data.map((unit) => (
                            <SelectItem key={unit.id} value={unit.id}>
                              {unit.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
              <DialogFooter>
                <Button type="submit" disabled={createMutation.isPending || updateMutation.isPending}>
                  {createMutation.isPending || updateMutation.isPending ? "Salvando..." : "Salvar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>

          {editingResource && (
            <div className="border-t pt-4">
              <p className="mb-2 text-sm font-medium">Foto</p>
              <div className="flex items-center gap-4">
                <div className="bg-muted flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-full border">
                  {photoPreviewUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element -- preview de upload local/URL dinamica da API, nao um asset estatico do build.
                    <img src={photoPreviewUrl} alt="" className="size-full object-cover" />
                  ) : (
                    <span className="text-muted-foreground text-xs">Sem foto</span>
                  )}
                </div>
                <div className="flex flex-col gap-2">
                  <input
                    ref={photoInputRef}
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    onChange={handlePhotoFileChange}
                    className="hidden"
                  />
                  <div className="flex gap-2">
                    <Button type="button" variant="outline" size="sm" onClick={() => photoInputRef.current?.click()}>
                      Escolher arquivo
                    </Button>
                    {selectedPhotoFile && (
                      <Button type="button" size="sm" onClick={handlePhotoUpload} disabled={isUploadingPhoto}>
                        {isUploadingPhoto ? "Enviando..." : "Enviar"}
                      </Button>
                    )}
                  </div>
                  <p className="text-muted-foreground text-xs">PNG, JPEG ou WEBP, ate 2MB.</p>
                </div>
              </div>

              <p className="mt-4 mb-2 text-sm font-medium">Especialidades</p>
              <div className="flex gap-2">
                <Input
                  placeholder="Corte, Barba, Coloracao"
                  value={specialtiesInput}
                  onChange={(event) => setSpecialtiesInput(event.target.value)}
                />
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => specialtiesMutation.mutate(parseSpecialties(specialtiesInput))}
                  disabled={specialtiesMutation.isPending}
                >
                  {specialtiesMutation.isPending ? "Salvando..." : "Salvar"}
                </Button>
              </div>
              <p className="text-muted-foreground mt-1 text-xs">Separe por virgula.</p>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={servicesDialogOpen} onOpenChange={setServicesDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Servicos de {servicesResource?.name}</DialogTitle>
          </DialogHeader>
          <p className="text-muted-foreground text-sm">
            Marque os servicos que este recurso pode realizar. Sem nenhum marcado, o recurso pode ser escalado para
            qualquer servico.
          </p>
          <div className="flex max-h-80 flex-col gap-2 overflow-y-auto">
            {allServicesQuery.isLoading ? (
              <Skeleton className="h-32 w-full" />
            ) : allServicesQuery.data?.items.length === 0 ? (
              <p className="text-muted-foreground text-sm">Nenhum servico cadastrado ainda.</p>
            ) : (
              allServicesQuery.data?.items.map((service) => (
                <label key={service.id} className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={selectedServiceIds.includes(service.id)}
                    onCheckedChange={() => toggleServiceId(service.id)}
                  />
                  {service.name}
                </label>
              ))
            )}
          </div>
          <DialogFooter>
            <Button
              onClick={() => servicesMutation.mutate(selectedServiceIds)}
              disabled={servicesMutation.isPending}
            >
              {servicesMutation.isPending ? "Salvando..." : "Salvar"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={timeOffDialogOpen} onOpenChange={setTimeOffDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Folgas de {timeOffResource?.name}</DialogTitle>
          </DialogHeader>
          <div className="flex flex-col gap-2">
            {timeOffsQuery.isLoading ? (
              <Skeleton className="h-16 w-full" />
            ) : timeOffsQuery.data?.length === 0 ? (
              <p className="text-muted-foreground text-sm">Nenhuma folga cadastrada ainda.</p>
            ) : (
              timeOffsQuery.data?.map((timeOff: TimeOffSummary) => (
                <div key={timeOff.id} className="flex items-center justify-between gap-2 rounded-md border p-2 text-sm">
                  <div>
                    <p>
                      {timeOff.startDate === timeOff.endDate
                        ? timeOff.startDate
                        : `${timeOff.startDate} a ${timeOff.endDate}`}
                    </p>
                    {timeOff.reason && <p className="text-muted-foreground text-xs">{timeOff.reason}</p>}
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label="Remover folga"
                    onClick={() => deleteTimeOffMutation.mutate(timeOff.id)}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              ))
            )}
          </div>

          <div className="flex flex-col gap-2 border-t pt-4">
            <div className="grid grid-cols-2 gap-2">
              <div className="grid gap-1">
                <label className="text-sm font-medium" htmlFor="time-off-start">
                  Inicio
                </label>
                <Input
                  id="time-off-start"
                  type="date"
                  value={newTimeOffStart}
                  onChange={(event) => setNewTimeOffStart(event.target.value)}
                />
              </div>
              <div className="grid gap-1">
                <label className="text-sm font-medium" htmlFor="time-off-end">
                  Fim
                </label>
                <Input
                  id="time-off-end"
                  type="date"
                  value={newTimeOffEnd}
                  onChange={(event) => setNewTimeOffEnd(event.target.value)}
                />
              </div>
            </div>
            <Input
              placeholder="Motivo (opcional)"
              value={newTimeOffReason}
              onChange={(event) => setNewTimeOffReason(event.target.value)}
            />
            <Button
              type="button"
              variant="outline"
              className="self-start"
              disabled={!newTimeOffStart || !newTimeOffEnd || createTimeOffMutation.isPending}
              onClick={() =>
                createTimeOffMutation.mutate({
                  startDate: newTimeOffStart,
                  endDate: newTimeOffEnd,
                  reason: toNullable(newTimeOffReason),
                })
              }
            >
              <Plus className="size-4" />
              {createTimeOffMutation.isPending ? "Salvando..." : "Adicionar folga"}
            </Button>
          </div>
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
    </div>
  );
}
