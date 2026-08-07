"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowLeft, ChevronLeft, ChevronRight } from "lucide-react";

import {
  listAppointments,
  scheduleAppointment,
  confirmAppointment,
  startAppointment,
  completeAppointment,
  markAppointmentNoShow,
  cancelAppointment,
  rescheduleAppointment,
  listResources,
  listCustomers,
  listServices,
  ApiError,
  type AppointmentSummary,
  type AppointmentStatus,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";

const DAY_START_HOUR = 7;
const DAY_END_HOUR = 21;
const SLOT_MINUTES = 30;
const ROW_HEIGHT_PX = 32;
const TOTAL_MINUTES = (DAY_END_HOUR - DAY_START_HOUR) * 60;
const GRID_HEIGHT_PX = (TOTAL_MINUTES / SLOT_MINUTES) * ROW_HEIGHT_PX;

const STATUS_LABELS: Record<AppointmentStatus, string> = {
  Scheduled: "Agendado",
  Confirmed: "Confirmado",
  InProgress: "Em andamento",
  Completed: "Concluido",
  NoShow: "Nao compareceu",
  CancelledByCustomer: "Cancelado (cliente)",
  CancelledByStaff: "Cancelado (equipe)",
};

const STATUS_VARIANTS: Record<AppointmentStatus, "default" | "secondary" | "outline" | "destructive"> = {
  Scheduled: "secondary",
  Confirmed: "default",
  InProgress: "default",
  Completed: "outline",
  NoShow: "destructive",
  CancelledByCustomer: "destructive",
  CancelledByStaff: "destructive",
};

const RESCHEDULABLE_STATUSES: AppointmentStatus[] = ["Scheduled", "Confirmed"];

function startOfDay(date: Date): Date {
  const d = new Date(date);
  d.setHours(0, 0, 0, 0);
  return d;
}

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function startOfWeek(date: Date): Date {
  const d = startOfDay(date);
  const day = d.getDay();
  const diff = (day === 0 ? -6 : 1) - day;
  return addDays(d, diff);
}

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function minutesFromDayStart(date: Date): number {
  return (date.getHours() - DAY_START_HOUR) * 60 + date.getMinutes();
}

function toDatetimeLocalValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

function formatDayLabel(date: Date): string {
  return date.toLocaleDateString("pt-BR", { day: "2-digit", month: "short" });
}

const createAppointmentSchema = z.object({
  customerId: z.string().min(1, "Selecione o cliente."),
  serviceId: z.string().min(1, "Selecione o servico."),
  startAtLocal: z.string().min(1, "Informe o horario."),
  notes: z.string(),
});

type CreateAppointmentFormValues = z.infer<typeof createAppointmentSchema>;

const rescheduleSchema = z.object({
  newStartAtLocal: z.string().min(1, "Informe o novo horario."),
});

type RescheduleFormValues = z.infer<typeof rescheduleSchema>;

export default function AgendaPage() {
  const router = useRouter();
  const { session } = useSession();
  const queryClient = useQueryClient();

  const [view, setView] = React.useState<"day" | "week" | "month">("day");
  const [currentDate, setCurrentDate] = React.useState(() => startOfDay(new Date()));
  const [selectedResourceId, setSelectedResourceId] = React.useState<string | null>(null);
  const [createDialogState, setCreateDialogState] = React.useState<{ resourceId: string; start: Date } | null>(null);
  const [selectedAppointmentId, setSelectedAppointmentId] = React.useState<string | null>(null);
  const [reschedulingOpen, setReschedulingOpen] = React.useState(false);
  const [draggingId, setDraggingId] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!session) {
      router.replace("/login");
    }
  }, [session, router]);

  const accessToken = session?.accessToken ?? "";

  const resourcesQuery = useQuery({
    queryKey: ["resources", { page: 1 }],
    queryFn: () => listResources({ page: 1, pageSize: 100 }, accessToken),
    enabled: Boolean(session),
  });

  const customersQuery = useQuery({
    queryKey: ["customers", { page: 1 }],
    queryFn: () => listCustomers({ page: 1, pageSize: 100 }, accessToken),
    enabled: Boolean(session),
  });

  const servicesQuery = useQuery({
    queryKey: ["services", { page: 1 }],
    queryFn: () => listServices({ page: 1, pageSize: 100 }, accessToken),
    enabled: Boolean(session),
  });

  const activeResources = React.useMemo(
    () => (resourcesQuery.data?.items ?? []).filter((r) => r.isActive),
    [resourcesQuery.data]
  );

  // Deriva o recurso selecionado em vez de sincronizar via efeito: evita um
  // re-render em cascata so para aplicar o default assim que os recursos chegam.
  const resolvedResourceId = selectedResourceId ?? activeResources[0]?.id ?? null;

  const range = React.useMemo(() => {
    if (view === "day") {
      const from = startOfDay(currentDate);
      return { from, to: addDays(from, 1) };
    }
    if (view === "week") {
      const from = startOfWeek(currentDate);
      return { from, to: addDays(from, 7) };
    }
    const from = startOfMonth(currentDate);
    return { from, to: new Date(from.getFullYear(), from.getMonth() + 1, 1) };
  }, [view, currentDate]);

  const appointmentsQuery = useQuery({
    queryKey: [
      "appointments",
      { from: range.from.toISOString(), to: range.to.toISOString(), resourceId: view === "week" ? resolvedResourceId : undefined },
    ],
    queryFn: () =>
      listAppointments(
        {
          fromUtc: range.from.toISOString(),
          toUtc: range.to.toISOString(),
          resourceId: view === "week" ? (resolvedResourceId ?? undefined) : undefined,
        },
        accessToken
      ),
    enabled: Boolean(session),
  });

  const customerNameById = React.useMemo(() => {
    const map = new Map<string, string>();
    for (const customer of customersQuery.data?.items ?? []) {
      map.set(customer.id, customer.fullName);
    }
    return map;
  }, [customersQuery.data]);

  const resourceNameById = React.useMemo(() => {
    const map = new Map<string, string>();
    for (const resource of activeResources) {
      map.set(resource.id, resource.name);
    }
    return map;
  }, [activeResources]);

  const invalidateAppointments = () => queryClient.invalidateQueries({ queryKey: ["appointments"] });

  const createForm = useForm<CreateAppointmentFormValues>({
    resolver: zodResolver(createAppointmentSchema),
    defaultValues: { customerId: "", serviceId: "", startAtLocal: "", notes: "" },
  });

  const rescheduleForm = useForm<RescheduleFormValues>({
    resolver: zodResolver(rescheduleSchema),
    defaultValues: { newStartAtLocal: "" },
  });

  const scheduleMutation = useMutation({
    mutationFn: (values: CreateAppointmentFormValues) => {
      if (!createDialogState) {
        throw new Error("Nenhum horario selecionado.");
      }
      return scheduleAppointment(
        {
          customerId: values.customerId,
          serviceId: values.serviceId,
          resourceId: createDialogState.resourceId,
          startAtUtc: new Date(values.startAtLocal).toISOString(),
          notes: values.notes.trim() === "" ? null : values.notes.trim(),
        },
        accessToken
      );
    },
    onSuccess: () => {
      toast.success("Agendamento criado.");
      invalidateAppointments();
      setCreateDialogState(null);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel criar o agendamento."),
  });

  const confirmMutation = useMutation({
    mutationFn: (id: string) => confirmAppointment(id, accessToken),
    onSuccess: () => {
      toast.success("Agendamento confirmado.");
      invalidateAppointments();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel confirmar."),
  });

  const startMutation = useMutation({
    mutationFn: (id: string) => startAppointment(id, accessToken),
    onSuccess: () => {
      toast.success("Atendimento iniciado.");
      invalidateAppointments();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel iniciar."),
  });

  const completeMutation = useMutation({
    mutationFn: (id: string) => completeAppointment(id, accessToken),
    onSuccess: () => {
      toast.success("Agendamento concluido.");
      invalidateAppointments();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel concluir."),
  });

  const noShowMutation = useMutation({
    mutationFn: (id: string) => markAppointmentNoShow(id, accessToken),
    onSuccess: () => {
      toast.success("Marcado como nao compareceu.");
      invalidateAppointments();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel marcar."),
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => cancelAppointment(id, true, accessToken),
    onSuccess: () => {
      toast.success("Agendamento cancelado.");
      invalidateAppointments();
      setSelectedAppointmentId(null);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cancelar."),
  });

  const rescheduleMutation = useMutation({
    mutationFn: ({ id, newStartAtUtc }: { id: string; newStartAtUtc: string }) => rescheduleAppointment(id, newStartAtUtc, accessToken),
    onSuccess: () => {
      toast.success("Agendamento remarcado.");
      invalidateAppointments();
      setReschedulingOpen(false);
      setSelectedAppointmentId(null);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel remarcar."),
  });

  function openCreateDialog(resourceId: string, start: Date) {
    setCreateDialogState({ resourceId, start });
    createForm.reset({ customerId: "", serviceId: "", startAtLocal: toDatetimeLocalValue(start), notes: "" });
  }

  function openDetailDialog(appointmentId: string) {
    setSelectedAppointmentId(appointmentId);
    setReschedulingOpen(false);
  }

  function handleDropOnSlot(resourceId: string, start: Date) {
    if (!draggingId) {
      return;
    }
    const appointment = appointmentsQuery.data?.find((a) => a.id === draggingId);
    setDraggingId(null);
    if (!appointment || !RESCHEDULABLE_STATUSES.includes(appointment.status)) {
      return;
    }
    // Drag & drop so muda o horario — trocar de recurso exigiria um comando
    // diferente no backend (Reschedule so aceita novo horario, nao novo
    // recurso), entao ignoramos solto fora da coluna original.
    if (resourceId !== appointment.resourceId) {
      toast.error("Arraste apenas dentro da mesma coluna para remarcar o horario.");
      return;
    }
    rescheduleMutation.mutate({ id: draggingId, newStartAtUtc: start.toISOString() });
  }

  const selectedAppointment = appointmentsQuery.data?.find((a) => a.id === selectedAppointmentId) ?? null;

  React.useEffect(() => {
    if (selectedAppointment) {
      rescheduleForm.reset({ newStartAtLocal: toDatetimeLocalValue(new Date(selectedAppointment.startUtc)) });
    }
  }, [selectedAppointment, rescheduleForm]);

  function renderTimeGrid(columns: { key: string; label: string; resourceId: string; date: Date }[]) {
    const slotCount = TOTAL_MINUTES / SLOT_MINUTES;

    return (
      <div className="overflow-x-auto rounded-lg border">
        <div className="grid" style={{ gridTemplateColumns: `4rem repeat(${columns.length}, minmax(9rem, 1fr))` }}>
          <div className="border-b border-r" />
          {columns.map((column) => (
            <div key={column.key} className="text-muted-foreground border-b border-r p-2 text-center text-xs font-medium last:border-r-0">
              {column.label}
            </div>
          ))}

          <div className="relative border-r" style={{ height: GRID_HEIGHT_PX }}>
            {Array.from({ length: slotCount }).map((_, index) => (
              <div
                key={index}
                className="text-muted-foreground absolute right-1 -translate-y-1/2 text-[10px]"
                style={{ top: index * ROW_HEIGHT_PX }}
              >
                {index % 2 === 0 ? `${String(DAY_START_HOUR + index / 2).padStart(2, "0")}:00` : ""}
              </div>
            ))}
          </div>

          {columns.map((column) => (
            <div key={column.key} className="relative border-r last:border-r-0" style={{ height: GRID_HEIGHT_PX }}>
              {Array.from({ length: slotCount }).map((_, index) => {
                const slotStart = new Date(column.date);
                slotStart.setHours(DAY_START_HOUR, 0, 0, 0);
                slotStart.setMinutes(slotStart.getMinutes() + index * SLOT_MINUTES);
                return (
                  <button
                    key={index}
                    type="button"
                    aria-label={`Novo agendamento em ${column.label} as ${slotStart.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}`}
                    className="hover:bg-muted/50 absolute inset-x-0 border-t first:border-t-0"
                    style={{ top: index * ROW_HEIGHT_PX, height: ROW_HEIGHT_PX }}
                    onClick={() => openCreateDialog(column.resourceId, slotStart)}
                    onDragOver={(event) => event.preventDefault()}
                    onDrop={(event) => {
                      event.preventDefault();
                      handleDropOnSlot(column.resourceId, slotStart);
                    }}
                  />
                );
              })}

              {(appointmentsQuery.data ?? [])
                .filter((appointment) => {
                  if (appointment.resourceId !== column.resourceId) {
                    return false;
                  }
                  const start = new Date(appointment.startUtc);
                  return startOfDay(start).getTime() === startOfDay(column.date).getTime();
                })
                .map((appointment) => {
                  const start = new Date(appointment.startUtc);
                  const end = new Date(appointment.endUtc);
                  const top = (minutesFromDayStart(start) / SLOT_MINUTES) * ROW_HEIGHT_PX;
                  const height = Math.max(((end.getTime() - start.getTime()) / 60000 / SLOT_MINUTES) * ROW_HEIGHT_PX, ROW_HEIGHT_PX / 2);
                  const isCancelledOrDone = !RESCHEDULABLE_STATUSES.includes(appointment.status) && appointment.status !== "InProgress";

                  return (
                    <button
                      key={appointment.id}
                      type="button"
                      draggable={RESCHEDULABLE_STATUSES.includes(appointment.status)}
                      onDragStart={() => setDraggingId(appointment.id)}
                      onDragEnd={() => setDraggingId(null)}
                      onClick={(event) => {
                        event.stopPropagation();
                        openDetailDialog(appointment.id);
                      }}
                      className={`absolute inset-x-0.5 z-10 overflow-hidden rounded-md border p-1 text-left text-[11px] leading-tight shadow-sm ${
                        isCancelledOrDone ? "bg-muted text-muted-foreground opacity-70" : "bg-primary/10 border-primary/30"
                      }`}
                      style={{ top, height }}
                    >
                      <p className="truncate font-medium">{customerNameById.get(appointment.customerId) ?? "Cliente"}</p>
                      <p className="truncate">{appointment.serviceName}</p>
                    </button>
                  );
                })}
            </div>
          ))}
        </div>
      </div>
    );
  }

  function renderMonthView() {
    const monthStart = startOfMonth(currentDate);
    const gridStart = startOfWeek(monthStart);
    const weeks = 6;
    const days = Array.from({ length: weeks * 7 }).map((_, index) => addDays(gridStart, index));

    const countsByDay = new Map<string, AppointmentSummary[]>();
    for (const appointment of appointmentsQuery.data ?? []) {
      const key = startOfDay(new Date(appointment.startUtc)).toISOString();
      const list = countsByDay.get(key) ?? [];
      list.push(appointment);
      countsByDay.set(key, list);
    }

    return (
      <div className="grid grid-cols-7 gap-2">
        {["Seg", "Ter", "Qua", "Qui", "Sex", "Sab", "Dom"].map((label) => (
          <div key={label} className="text-muted-foreground text-center text-xs font-medium">
            {label}
          </div>
        ))}
        {days.map((day) => {
          const isCurrentMonth = day.getMonth() === monthStart.getMonth();
          const dayAppointments = countsByDay.get(startOfDay(day).toISOString()) ?? [];

          return (
            <button
              key={day.toISOString()}
              type="button"
              onClick={() => {
                setCurrentDate(startOfDay(day));
                setView("day");
              }}
              className={`min-h-24 rounded-lg border p-2 text-left text-xs hover:bg-muted/50 ${isCurrentMonth ? "" : "text-muted-foreground opacity-50"}`}
            >
              <p className="mb-1 font-medium">{day.getDate()}</p>
              {dayAppointments.length > 0 && (
                <Badge variant="secondary" className="text-[10px]">
                  {dayAppointments.length} agendamento(s)
                </Badge>
              )}
            </button>
          );
        })}
      </div>
    );
  }

  if (!session) {
    return null;
  }

  const dayColumns = activeResources.map((resource) => ({
    key: resource.id,
    label: resource.name,
    resourceId: resource.id,
    date: currentDate,
  }));

  const weekColumns = Array.from({ length: 7 }).map((_, index) => {
    const date = addDays(range.from, index);
    return {
      key: date.toISOString(),
      label: formatDayLabel(date),
      resourceId: resolvedResourceId ?? "",
      date,
    };
  });

  const rangeLabel =
    view === "day"
      ? currentDate.toLocaleDateString("pt-BR", { weekday: "long", day: "2-digit", month: "long", year: "numeric" })
      : view === "week"
        ? `${formatDayLabel(range.from)} - ${formatDayLabel(addDays(range.to, -1))}`
        : currentDate.toLocaleDateString("pt-BR", { month: "long", year: "numeric" });

  function navigate(direction: -1 | 1) {
    if (view === "day") {
      setCurrentDate((current) => addDays(current, direction));
    } else if (view === "week") {
      setCurrentDate((current) => addDays(current, direction * 7));
    } else {
      setCurrentDate((current) => new Date(current.getFullYear(), current.getMonth() + direction, 1));
    }
  }

  return (
    <main className="mx-auto flex min-h-full w-full max-w-6xl flex-1 flex-col p-6 sm:p-10">
      <Link href="/painel" className="text-muted-foreground mb-6 inline-flex items-center gap-1.5 text-sm hover:text-foreground">
        <ArrowLeft className="size-4" />
        Voltar
      </Link>

      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Agenda</h1>
          <p className="text-muted-foreground mt-1 text-sm capitalize">{rangeLabel}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {view === "week" && (
            <Select value={resolvedResourceId ?? undefined} onValueChange={setSelectedResourceId}>
              <SelectTrigger className="w-40">
                <SelectValue placeholder="Recurso" />
              </SelectTrigger>
              <SelectContent>
                {activeResources.map((resource) => (
                  <SelectItem key={resource.id} value={resource.id}>
                    {resource.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          <div className="flex overflow-hidden rounded-lg border">
            <Button variant="ghost" size="sm" className="rounded-none" onClick={() => navigate(-1)} aria-label="Anterior">
              <ChevronLeft className="size-4" />
            </Button>
            <Button variant="ghost" size="sm" className="rounded-none border-x" onClick={() => setCurrentDate(startOfDay(new Date()))}>
              Hoje
            </Button>
            <Button variant="ghost" size="sm" className="rounded-none" onClick={() => navigate(1)} aria-label="Proximo">
              <ChevronRight className="size-4" />
            </Button>
          </div>
          <div className="flex overflow-hidden rounded-lg border">
            {(["day", "week", "month"] as const).map((option) => (
              <Button
                key={option}
                variant={view === option ? "default" : "ghost"}
                size="sm"
                className="rounded-none"
                onClick={() => setView(option)}
              >
                {option === "day" ? "Dia" : option === "week" ? "Semana" : "Mes"}
              </Button>
            ))}
          </div>
        </div>
      </div>

      {activeResources.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Cadastre ao menos um recurso ativo em <Link href="/recursos" className="underline">Recursos</Link> para comecar a agendar.
        </p>
      ) : view === "month" ? (
        renderMonthView()
      ) : view === "day" ? (
        renderTimeGrid(dayColumns)
      ) : (
        renderTimeGrid(weekColumns)
      )}

      <Dialog open={Boolean(createDialogState)} onOpenChange={(open) => !open && setCreateDialogState(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Novo agendamento</DialogTitle>
          </DialogHeader>
          <Form {...createForm}>
            <form onSubmit={createForm.handleSubmit((values) => scheduleMutation.mutate(values))} className="flex flex-col gap-3">
              <FormField
                control={createForm.control}
                name="customerId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Cliente</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecione um cliente" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {(customersQuery.data?.items ?? []).map((customer) => (
                          <SelectItem key={customer.id} value={customer.id}>
                            {customer.fullName}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={createForm.control}
                name="serviceId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Servico</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecione um servico" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {(servicesQuery.data?.items ?? []).map((service) => (
                          <SelectItem key={service.id} value={service.id}>
                            {service.name} ({service.durationMinutes} min)
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={createForm.control}
                name="startAtLocal"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Data e horario</FormLabel>
                    <FormControl>
                      <Input type="datetime-local" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={createForm.control}
                name="notes"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Observacoes</FormLabel>
                    <FormControl>
                      <Textarea rows={2} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <DialogFooter>
                <Button type="submit" disabled={scheduleMutation.isPending}>
                  {scheduleMutation.isPending ? "Salvando..." : "Agendar"}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(selectedAppointment)} onOpenChange={(open) => !open && setSelectedAppointmentId(null)}>
        <DialogContent className="sm:max-w-md">
          {selectedAppointment && (
            <>
              <DialogHeader>
                <DialogTitle>{customerNameById.get(selectedAppointment.customerId) ?? "Cliente"}</DialogTitle>
              </DialogHeader>
              <div className="flex flex-col gap-2 text-sm">
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Status</span>
                  <Badge variant={STATUS_VARIANTS[selectedAppointment.status]}>{STATUS_LABELS[selectedAppointment.status]}</Badge>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Servico</span>
                  <span>{selectedAppointment.serviceName}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Recurso</span>
                  <span>{resourceNameById.get(selectedAppointment.resourceId) ?? "—"}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Horario</span>
                  <span>
                    {new Date(selectedAppointment.startUtc).toLocaleDateString("pt-BR")} {formatTime(selectedAppointment.startUtc)} -{" "}
                    {formatTime(selectedAppointment.endUtc)}
                  </span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Preco</span>
                  <span>
                    {new Intl.NumberFormat("pt-BR", { style: "currency", currency: selectedAppointment.currency }).format(
                      selectedAppointment.price
                    )}
                  </span>
                </div>
                {selectedAppointment.notes && <p className="text-muted-foreground italic">{selectedAppointment.notes}</p>}
              </div>

              {reschedulingOpen ? (
                <Form {...rescheduleForm}>
                  <form
                    onSubmit={rescheduleForm.handleSubmit((values) =>
                      rescheduleMutation.mutate({ id: selectedAppointment.id, newStartAtUtc: new Date(values.newStartAtLocal).toISOString() })
                    )}
                    className="flex flex-col gap-3"
                  >
                    <FormField
                      control={rescheduleForm.control}
                      name="newStartAtLocal"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Novo horario</FormLabel>
                          <FormControl>
                            <Input type="datetime-local" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <div className="flex gap-2">
                      <Button type="submit" size="sm" disabled={rescheduleMutation.isPending}>
                        {rescheduleMutation.isPending ? "Salvando..." : "Confirmar nova data"}
                      </Button>
                      <Button type="button" variant="ghost" size="sm" onClick={() => setReschedulingOpen(false)}>
                        Cancelar
                      </Button>
                    </div>
                  </form>
                </Form>
              ) : (
                <DialogFooter className="flex-wrap justify-start!">
                  {selectedAppointment.status === "Scheduled" && (
                    <Button size="sm" onClick={() => confirmMutation.mutate(selectedAppointment.id)}>
                      Confirmar
                    </Button>
                  )}
                  {(selectedAppointment.status === "Scheduled" || selectedAppointment.status === "Confirmed") && (
                    <Button size="sm" variant="outline" onClick={() => startMutation.mutate(selectedAppointment.id)}>
                      Iniciar
                    </Button>
                  )}
                  {selectedAppointment.status === "InProgress" && (
                    <Button size="sm" onClick={() => completeMutation.mutate(selectedAppointment.id)}>
                      Concluir
                    </Button>
                  )}
                  {RESCHEDULABLE_STATUSES.includes(selectedAppointment.status) && (
                    <>
                      <Button size="sm" variant="outline" onClick={() => setReschedulingOpen(true)}>
                        Remarcar
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => noShowMutation.mutate(selectedAppointment.id)}>
                        Nao compareceu
                      </Button>
                      <Button size="sm" variant="destructive" onClick={() => cancelMutation.mutate(selectedAppointment.id)}>
                        Cancelar
                      </Button>
                    </>
                  )}
                </DialogFooter>
              )}
            </>
          )}
        </DialogContent>
      </Dialog>
    </main>
  );
}
