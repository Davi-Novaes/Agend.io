"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useQueries, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { CalendarDays, ChevronLeft, ChevronRight, Plus, Search, Users, X } from "lucide-react";

import {
  listAppointments,
  scheduleAppointment,
  confirmAppointment,
  startAppointment,
  completeAppointment,
  markAppointmentNoShow,
  cancelAppointment,
  rescheduleAppointment,
  listAppointmentChangeLog,
  getAppointmentDeposit,
  listResources,
  listCustomers,
  createCustomer,
  listServices,
  listUnits,
  getResourceById,
  listTimeOffs,
  ApiError,
  type AppointmentSummary,
  type AppointmentStatus,
  type ResourceSummary,
  CUSTOMER_RECOVERY_QUERY_KEY,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import { APPOINTMENT_STATUS_LABELS, APPOINTMENT_STATUS_VARIANTS } from "@/lib/appointment-status";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { EmptyState } from "@/components/ui/empty-state";
import { AgendaKpiRow } from "@/components/agenda/kpi-row";
import { NextAppointmentCard } from "@/components/agenda/next-appointment-card";
import { ResourceColumnHeader, type ResourceAvailability } from "@/components/agenda/resource-column-header";
import { AppointmentChip } from "@/components/agenda/appointment-chip";
import { BreakBand, type BlockedBand } from "@/components/agenda/break-band";
import { AgendaFiltersPopover, isFiltersEmpty, type AgendaFilters } from "@/components/agenda/filters-popover";
import { AgendaToolbarFooter, DEFAULT_ZOOM_PX, type Density } from "@/components/agenda/agenda-toolbar-footer";

// Sentinela pro item "+ Novo cliente" dentro do proprio Select de cliente do
// modal de agendamento — nunca colide com um Id real (GUID). Cenario comum
// de barbearia/salao: walk-in pedindo horario na hora, sem cliente cadastrado
// ainda (BL-13, docs/BACKLOG.md).
const NEW_CUSTOMER_VALUE = "__new_customer__";

// Grade cobre o dia inteiro (00:00 -> 23:59): da pra marcar em qualquer
// horario, sem faixa de "fora do expediente" ocupando espaco. O expediente
// real vira so um rotulo informativo no topo da coluna de horarios.
const DAY_START_HOUR = 0;
const DAY_END_HOUR = 24;
const SLOT_MINUTES = 30;
// Alvo minimo de toque recomendado pelo WCAG 2.2 (criterio 2.5.8, AA) — sem
// isso, um agendamento curto (ex. Sobrancelha, 15min) rendia um chip de
// 16px de altura, abaixo do minimo, relevante pra uso em tablet/touch na
// recepcao (BL-19, docs/BACKLOG.md). Fixo independente do zoom escolhido —
// o alvo de toque e um requisito absoluto, nao proporcional.
const MIN_CHIP_HEIGHT_PX = 24;

const STATUS_LABELS = APPOINTMENT_STATUS_LABELS;
const STATUS_VARIANTS = APPOINTMENT_STATUS_VARIANTS;

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

function minutesFromDayStart(date: Date, dayStartHour: number): number {
  return (date.getHours() - dayStartHour) * 60 + date.getMinutes();
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

// Proximo slot de 30min dentro do expediente, a partir de agora — usado
// tanto pelo deep link ?novo=1 (vindo do Painel) quanto pelo botao "+ Novo
// agendamento" do cabecalho, pra nao duplicar a mesma logica duas vezes.
function nextAvailableSlot(dayStartHour: number, dayEndHour: number): Date {
  const now = new Date();
  const start = new Date(now);
  start.setSeconds(0, 0);
  start.setMinutes(Math.ceil(now.getMinutes() / SLOT_MINUTES) * SLOT_MINUTES);
  if (start.getHours() < dayStartHour || start.getHours() >= dayEndHour) {
    start.setHours(dayStartHour, 0, 0, 0);
    if (start.getTime() <= now.getTime()) {
      start.setDate(start.getDate() + 1);
    }
  }
  return start;
}

function formatHourLabel(hour: number, minutes: number): string {
  return `${String(hour).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
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
  reason: z.string(),
});

type RescheduleFormValues = z.infer<typeof rescheduleSchema>;

const cancelSchema = z.object({
  reason: z.string(),
});

type CancelFormValues = z.infer<typeof cancelSchema>;

function formatChangeLogDateTime(value: string): string {
  return new Date(value).toLocaleString("pt-BR");
}

export default function AgendaPage() {
  const { session } = useSession();
  const queryClient = useQueryClient();
  const router = useRouter();

  const [view, setView] = React.useState<"day" | "week" | "month">("day");
  const [currentDate, setCurrentDate] = React.useState(() => startOfDay(new Date()));
  const [selectedResourceId, setSelectedResourceId] = React.useState<string | null>(null);
  const [selectedUnitId, setSelectedUnitId] = React.useState<string | null>(null);
  const [createDialogState, setCreateDialogState] = React.useState<{ resourceId: string; start: Date } | null>(null);
  const [selectedAppointmentId, setSelectedAppointmentId] = React.useState<string | null>(null);
  const [reschedulingOpen, setReschedulingOpen] = React.useState(false);
  // Fora do rescheduleForm (zod) de proposito — reatribuir e opcional e nao
  // tem validacao propria, so precisa saber "mudou ou nao" no submit.
  const [reassignResourceId, setReassignResourceId] = React.useState<string | null>(null);
  const [cancelingOpen, setCancelingOpen] = React.useState(false);
  const [draggingId, setDraggingId] = React.useState<string | null>(null);
  // Drag-and-drop HTML5 nao tem equivalente em touch — em vez de oferecer um
  // atalho que simplesmente nao faz nada (sem nenhum aviso) num tablet/celular,
  // detecta o ponteiro grosseiro e desliga o "draggable" por completo: o toque
  // continua abrindo o dialog de detalhe normalmente, que ja tem "Remarcar"
  // 100% acessivel por clique/teclado/touch (BL-08, docs/BACKLOG.md).
  const [isCoarsePointer] = React.useState(
    () => typeof window !== "undefined" && window.matchMedia("(pointer: coarse)").matches
  );
  const [selectedIds, setSelectedIds] = React.useState<Set<string>>(new Set());
  const [searchQuery, setSearchQuery] = React.useState("");
  const [filters, setFilters] = React.useState<AgendaFilters>({
    statuses: new Set(),
    serviceIds: new Set(),
    resourceIds: new Set(),
  });
  const [rowHeightPx, setRowHeightPx] = React.useState(DEFAULT_ZOOM_PX);
  const [density, setDensity] = React.useState<Density>("normal");
  const gridScrollRef = React.useRef<HTMLDivElement | null>(null);
  const hasAutoScrolled = React.useRef(false);

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

  const unitsQuery = useQuery({
    queryKey: ["units"],
    queryFn: () => listUnits(accessToken),
    enabled: Boolean(session),
  });

  // O filtro de unidade so aparece com mais de uma cadastrada — tenant de
  // unidade unica nao deveria ver um controle inutil (CLAUDE.md: defaults
  // sensatos sem o dono precisar configurar nada).
  const showUnitFilter = (unitsQuery.data?.length ?? 0) > 1;

  const activeResources = React.useMemo(() => {
    const active = (resourcesQuery.data?.items ?? []).filter((r) => r.isActive);
    return showUnitFilter && selectedUnitId ? active.filter((r) => r.unitId === selectedUnitId) : active;
  }, [resourcesQuery.data, showUnitFilter, selectedUnitId]);

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
      {
        from: range.from.toISOString(),
        to: range.to.toISOString(),
        resourceId: view === "week" ? resolvedResourceId : undefined,
        unitId: showUnitFilter ? selectedUnitId : undefined,
      },
    ],
    queryFn: () =>
      listAppointments(
        {
          fromUtc: range.from.toISOString(),
          toUtc: range.to.toISOString(),
          resourceId: view === "week" ? (resolvedResourceId ?? undefined) : undefined,
          unitId: showUnitFilter && selectedUnitId ? selectedUnitId : undefined,
        },
        accessToken
      ),
    enabled: Boolean(session),
  });

  // Sempre "hoje", independente do dia/semana/mes selecionado no grid acima —
  // mesmo padrao do Painel (todayAppointmentsQuery em painel/page.tsx) pra
  // alimentar os cards de resumo/proximo atendimento sem misturar com o que
  // esta sendo navegado no momento.
  const todayStart = React.useMemo(() => startOfDay(new Date()), []);
  const todayEnd = React.useMemo(() => addDays(todayStart, 1), [todayStart]);
  const todayAppointmentsQuery = useQuery({
    queryKey: ["appointments", "hoje", todayStart.toISOString()],
    queryFn: () => listAppointments({ fromUtc: todayStart.toISOString(), toUtc: todayEnd.toISOString() }, accessToken),
    enabled: Boolean(session),
  });

  // Horario de trabalho e folga NAO vem em listResources (so em getResourceById) —
  // busca por recurso ativo, em paralelo, pra desenhar intervalo/fora-do-expediente/
  // ausente na grade com dado real (nunca fabricado).
  const workingHoursQueries = useQueries({
    queries: activeResources.map((resource) => ({
      queryKey: ["resource-details", resource.id],
      queryFn: () => getResourceById(resource.id, accessToken),
      enabled: Boolean(session),
    })),
  });
  const timeOffQueries = useQueries({
    queries: activeResources.map((resource) => ({
      queryKey: ["resource-timeoff", resource.id],
      queryFn: () => listTimeOffs(resource.id, accessToken),
      enabled: Boolean(session),
    })),
  });

  const workingHoursByResourceId = React.useMemo(() => {
    const map = new Map<string, { dayOfWeek: string; startTime: string; endTime: string }[]>();
    activeResources.forEach((resource, index) => {
      map.set(resource.id, workingHoursQueries[index]?.data?.workingHours ?? []);
    });
    return map;
  }, [activeResources, workingHoursQueries]);

  // A grade e sempre 24h (ver DAY_START_HOUR/DAY_END_HOUR). Isto aqui e so o
  // rotulo informativo "Horario funcionamento HH:MM - HH:MM" no topo da coluna
  // de horarios — a menor abertura e o maior fechamento entre os profissionais.
  const workingHoursLabel = React.useMemo(() => {
    let startHour = 24;
    let endHour = 0;

    for (const windows of workingHoursByResourceId.values()) {
      for (const window of windows) {
        const [windowStartHour] = window.startTime.split(":").map(Number);
        const [windowEndHour, windowEndMinutes] = window.endTime.split(":").map(Number);
        startHour = Math.min(startHour, windowStartHour);
        endHour = Math.max(endHour, windowEndMinutes > 0 ? windowEndHour + 1 : windowEndHour);
      }
    }

    return startHour < endHour ? { start: startHour, end: endHour } : null;
  }, [workingHoursByResourceId]);

  const totalMinutes = (DAY_END_HOUR - DAY_START_HOUR) * 60;

  const timeOffByResourceId = React.useMemo(() => {
    const map = new Map<string, { startDate: string; endDate: string }[]>();
    activeResources.forEach((resource, index) => {
      map.set(resource.id, timeOffQueries[index]?.data ?? []);
    });
    return map;
  }, [activeResources, timeOffQueries]);

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

  // Busca + filtros sao 100% client-side sobre o que ja esta carregado — sem
  // endpoint novo. Chips fora do filtro simplesmente nao renderizam na grade.
  const visibleAppointments = React.useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return (appointmentsQuery.data ?? []).filter((appointment) => {
      if (filters.statuses.size > 0 && !filters.statuses.has(appointment.status)) {
        return false;
      }
      if (filters.serviceIds.size > 0 && !filters.serviceIds.has(appointment.serviceId)) {
        return false;
      }
      if (filters.resourceIds.size > 0 && !filters.resourceIds.has(appointment.resourceId)) {
        return false;
      }
      if (query) {
        const customerName = (customerNameById.get(appointment.customerId) ?? "").toLowerCase();
        const serviceName = appointment.serviceName.toLowerCase();
        if (!customerName.includes(query) && !serviceName.includes(query)) {
          return false;
        }
      }
      return true;
    });
  }, [appointmentsQuery.data, filters, searchQuery, customerNameById]);

  function computeResourceAvailability(resourceId: string): ResourceAvailability {
    const now = new Date();
    const todayIso = startOfDay(now).toISOString().slice(0, 10);
    const onTimeOff = (timeOffByResourceId.get(resourceId) ?? []).some(
      (timeOff) => timeOff.startDate <= todayIso && todayIso <= timeOff.endDate
    );
    if (onTimeOff) {
      return "Ausente";
    }

    const inProgress = (appointmentsQuery.data ?? []).some(
      (appointment) => appointment.resourceId === resourceId && appointment.status === "InProgress"
    );
    if (inProgress) {
      return "EmAtendimento";
    }

    const dayOfWeekName = now.toLocaleDateString("en-US", { weekday: "long" });
    const nowTime = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}:00`;
    const windows = workingHoursByResourceId.get(resourceId) ?? [];
    const withinWorkingHours = windows.some(
      (w) => w.dayOfWeek === dayOfWeekName && w.startTime <= nowTime && nowTime <= w.endTime
    );
    return withinWorkingHours ? "Disponivel" : null;
  }

  function computeBlockedBands(resourceId: string, columnDate: Date, rowHeightPxValue: number): BlockedBand[] {
    const localIso = columnDate.toISOString().slice(0, 10);
    const onTimeOff = (timeOffByResourceId.get(resourceId) ?? []).some(
      (timeOff) => timeOff.startDate <= localIso && localIso <= timeOff.endDate
    );
    const gridHeightPx = (totalMinutes / SLOT_MINUTES) * rowHeightPxValue;
    if (onTimeOff) {
      return [{ top: 0, height: gridHeightPx, kind: "Ausente" }];
    }

    const dayOfWeekName = columnDate.toLocaleDateString("en-US", { weekday: "long" });
    const windows = (workingHoursByResourceId.get(resourceId) ?? [])
      .filter((w) => w.dayOfWeek === dayOfWeekName)
      .map((w) => ({ start: w.startTime, end: w.endTime }))
      .sort((a, b) => a.start.localeCompare(b.start));

    if (windows.length === 0) {
      return [];
    }

    function timeToTop(time: string): number {
      const [h, m] = time.split(":").map(Number);
      const minutesFromStart = (h - DAY_START_HOUR) * 60 + m;
      return Math.max(0, Math.min(gridHeightPx, (minutesFromStart / SLOT_MINUTES) * rowHeightPxValue));
    }

    function toShortTime(time: string): string {
      return time.slice(0, 5);
    }

    // So intervalo (buraco entre duas janelas do MESMO dia, ex. almoco). Fora
    // do expediente nao vira faixa: a grade e 24h e marcar fora do horario
    // padrao e permitido — hachurar o dia inteiro so poluiria a tela.
    const bands: BlockedBand[] = [];
    for (let i = 0; i < windows.length - 1; i += 1) {
      const gapTop = timeToTop(windows[i].end);
      const gapBottom = timeToTop(windows[i + 1].start);
      if (gapBottom > gapTop) {
        bands.push({
          top: gapTop,
          height: gapBottom - gapTop,
          kind: "Intervalo",
          timeRange: `${toShortTime(windows[i].end)} - ${toShortTime(windows[i + 1].start)}`,
        });
      }
    }

    return bands;
  }

  // So folga de dia inteiro bloqueia o clique — e o unico caso que o backend
  // de fato rejeita (AppointmentAvailabilityGuard). Intervalo fica marcado
  // visualmente, mas continua clicavel: encaixar um atendimento no almoco e
  // decisao do dono, nao erro.
  function isMinuteBlocked(bands: BlockedBand[], top: number): boolean {
    return bands.some((band) => band.kind === "Ausente" && top >= band.top && top < band.top + band.height);
  }

  // Tambem invalida a recuperacao de clientes: ela e calculada a partir do
  // ultimo atendimento concluido e de agendamentos futuros, entao qualquer
  // mutacao de agendamento (concluir, criar, cancelar, remarcar, faltou) pode
  // tirar ou colocar um cliente na lista de "ausentes" — sem isto, o cliente
  // continuava marcado como ausente no Painel/Clientes ate um F5.
  const invalidateAppointments = () => {
    queryClient.invalidateQueries({ queryKey: ["appointments"] });
    queryClient.invalidateQueries({ queryKey: CUSTOMER_RECOVERY_QUERY_KEY });
  };

  const createForm = useForm<CreateAppointmentFormValues>({
    resolver: zodResolver(createAppointmentSchema),
    defaultValues: { customerId: "", serviceId: "", startAtLocal: "", notes: "" },
  });

  // Cadastro rapido de cliente sem sair do modal de agendamento — mesma
  // funcao createCustomer que /clientes usa, so com os 2 campos que um
  // walk-in/ligacao normalmente ja tem na hora (perfil completo continua
  // editavel depois em Clientes). BL-13, docs/BACKLOG.md.
  const [quickCustomerOpen, setQuickCustomerOpen] = React.useState(false);
  const [quickCustomerName, setQuickCustomerName] = React.useState("");
  const [quickCustomerPhone, setQuickCustomerPhone] = React.useState("");

  const quickCreateCustomerMutation = useMutation({
    mutationFn: () => createCustomer({ fullName: quickCustomerName.trim(), phone: quickCustomerPhone.trim() || null }, accessToken),
    onSuccess: async (result) => {
      toast.success("Cliente cadastrado.");
      // Espera o refetch da lista TERMINAR E ASSENTAR antes de selecionar.
      // Bug conhecido do Radix Select (via SelectBubbleInput, o <select>
      // nativo espelhado por acessibilidade): setar o valor no MESMO instante
      // em que a lista de itens muda faz o proprio componente reverter o
      // campo pra vazio (o <select> nativo perde a opcao selecionada quando
      // o DOM de <option> e recriado, e o Radix propaga esse reset de volta).
      // Um instante extra depois do refetch resolver evita a corrida.
      await queryClient.invalidateQueries({ queryKey: ["customers"] });
      await new Promise((resolve) => setTimeout(resolve, 50));
      createForm.setValue("customerId", result.id, { shouldValidate: true });
      setQuickCustomerOpen(false);
      setQuickCustomerName("");
      setQuickCustomerPhone("");
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cadastrar o cliente."),
  });

  const rescheduleForm = useForm<RescheduleFormValues>({
    resolver: zodResolver(rescheduleSchema),
    defaultValues: { newStartAtLocal: "", reason: "" },
  });

  const cancelForm = useForm<CancelFormValues>({
    resolver: zodResolver(cancelSchema),
    defaultValues: { reason: "" },
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

  const changeLogQuery = useQuery({
    queryKey: ["appointment-history", selectedAppointmentId],
    queryFn: () => listAppointmentChangeLog({ appointmentId: selectedAppointmentId!, pageSize: 10 }, accessToken),
    enabled: Boolean(selectedAppointmentId),
  });

  const depositQuery = useQuery({
    queryKey: ["appointment-deposit", selectedAppointmentId],
    queryFn: () => getAppointmentDeposit(selectedAppointmentId!, accessToken),
    enabled: Boolean(selectedAppointmentId),
  });

  const cancelMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string | null }) => cancelAppointment(id, true, reason, accessToken),
    onSuccess: () => {
      toast.success("Agendamento cancelado.");
      invalidateAppointments();
      setCancelingOpen(false);
      setSelectedAppointmentId(null);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel cancelar."),
  });

  const rescheduleMutation = useMutation({
    mutationFn: ({
      id,
      newStartAtUtc,
      reason,
      newDurationMinutes,
      newResourceId,
    }: {
      id: string;
      newStartAtUtc: string;
      reason: string | null;
      newDurationMinutes?: number;
      newResourceId?: string;
    }) => rescheduleAppointment(id, { newStartAtUtc, reason, newDurationMinutes, newResourceId }, accessToken),
    onSuccess: () => {
      toast.success("Agendamento remarcado.");
      invalidateAppointments();
      queryClient.invalidateQueries({ queryKey: ["appointment-history"] });
      setReschedulingOpen(false);
      setSelectedAppointmentId(null);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Nao foi possivel remarcar."),
  });

  function openCreateDialog(resourceId: string, start: Date) {
    setCreateDialogState({ resourceId, start });
    createForm.reset({ customerId: "", serviceId: "", startAtLocal: toDatetimeLocalValue(start), notes: "" });
  }

  // Entrada rapida vinda do Dashboard ("+ Novo agendamento") — mesmo dialog e
  // mutation de sempre, so aberto automaticamente com um horario default (proximo
  // slot de 30min dentro do expediente) em vez do usuario clicar numa celula da grade.
  // Le window.location direto (em vez de useSearchParams) pra nao exigir um
  // boundary de Suspense so por causa desse efeito de montagem.
  // Guarda de "so uma vez": o efeito depende de dado assincrono e sem
  // isto reabriria o dialog que o usuario acabou de fechar.
  const hasHandledNovoParam = React.useRef(false);

  React.useEffect(() => {
    if (
      hasHandledNovoParam.current ||
      typeof window === "undefined" ||
      new URLSearchParams(window.location.search).get("novo") !== "1" ||
      !resolvedResourceId
    ) {
      return;
    }
    hasHandledNovoParam.current = true;

    const start = nextAvailableSlot(DAY_START_HOUR, DAY_END_HOUR);

    // Legitimo "esperar um dado assincrono (recursos) chegar, entao sincronizar" —
    // nao da pra resolver via useState(() => ...) porque resolvedResourceId so
    // existe depois que resourcesQuery volta da API.
    setCreateDialogState({ resourceId: resolvedResourceId, start });
    createForm.reset({ customerId: "", serviceId: "", startAtLocal: toDatetimeLocalValue(start), notes: "" });
    router.replace("/agenda");
  }, [resolvedResourceId, router, createForm]);

  function openDetailDialog(appointmentId: string) {
    setSelectedAppointmentId(appointmentId);
    setReschedulingOpen(false);
    setCancelingOpen(false);
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
    // Soltar numa coluna diferente reatribui o profissional (rescheduleAppointment
    // aceita newResourceId desde a extensao do redesign da Agenda) — mesma
    // chamada de sempre, so com o campo extra quando de fato mudou de coluna.
    rescheduleMutation.mutate({
      id: draggingId,
      newStartAtUtc: start.toISOString(),
      reason: null,
      newResourceId: resourceId !== appointment.resourceId ? resourceId : undefined,
    });
  }

  function handleQuickStatusAction(id: string, action: "confirm" | "start" | "complete" | "noshow" | "cancel") {
    if (action === "confirm") confirmMutation.mutate(id);
    else if (action === "start") startMutation.mutate(id);
    else if (action === "complete") completeMutation.mutate(id);
    else if (action === "noshow") noShowMutation.mutate(id);
    else cancelMutation.mutate({ id, reason: null });
  }

  function handleResizeCommit(id: string, newDurationMinutes: number) {
    const appointment = appointmentsQuery.data?.find((a) => a.id === id);
    if (!appointment) {
      return;
    }
    rescheduleMutation.mutate({ id, newStartAtUtc: appointment.startUtc, reason: null, newDurationMinutes });
  }

  function toggleSelection(id: string) {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  async function handleBulkAction(action: "confirm" | "cancel") {
    const ids = Array.from(selectedIds);
    const results = await Promise.allSettled(
      ids.map((id) => (action === "confirm" ? confirmAppointment(id, accessToken) : cancelAppointment(id, true, null, accessToken)))
    );
    const failures = results.filter((r) => r.status === "rejected").length;
    invalidateAppointments();
    setSelectedIds(new Set());
    if (failures > 0) {
      toast.error(`${failures} de ${ids.length} nao puderam ser processados.`);
    } else {
      toast.success(action === "confirm" ? "Agendamentos confirmados." : "Agendamentos cancelados.");
    }
  }

  const selectedAppointment = appointmentsQuery.data?.find((a) => a.id === selectedAppointmentId) ?? null;

  React.useEffect(() => {
    if (selectedAppointment) {
      rescheduleForm.reset({ newStartAtLocal: toDatetimeLocalValue(new Date(selectedAppointment.startUtc)) });
    }
  }, [selectedAppointment, rescheduleForm]);

  // A grade cobre 24h, mas abrir em 00:00 mostraria so madrugada vazia — rola
  // ate o comeco do expediente (ou 07:00 se ninguem configurou horario) na
  // primeira vez que o grid monta com dados.
  const isGridReady = !resourcesQuery.isLoading && !appointmentsQuery.isLoading && activeResources.length > 0;

  React.useEffect(() => {
    const container = gridScrollRef.current;
    if (hasAutoScrolled.current || !container || !isGridReady) {
      return;
    }
    hasAutoScrolled.current = true;
    const targetHour = workingHoursLabel?.start ?? 7;
    container.scrollTop = (((targetHour - DAY_START_HOUR) * 60) / SLOT_MINUTES) * rowHeightPx;
  }, [isGridReady, workingHoursLabel, rowHeightPx]);

  function renderTimeGrid(
    columns: { key: string; label: string; resourceId: string; date: Date; resource?: ResourceSummary }[],
    options: { showAddResourceColumn?: boolean } = {}
  ) {
    const slotCount = totalMinutes / SLOT_MINUTES;
    const gridHeightPx = slotCount * rowHeightPx;
    const selectionMode = selectedIds.size > 0;
    const now = new Date();
    const columnCount = columns.length + (options.showAddResourceColumn ? 1 : 0);

    return (
      // Rola nos dois eixos: 24h de grade nao cabe na tela, e o cabecalho de
      // profissionais/coluna de horas ficam presos (sticky) pra nao se perder
      // a referencia ao rolar.
      <div ref={gridScrollRef} className="scroll-shadow-x max-h-[calc(100vh-19rem)] min-h-96 overflow-auto rounded-lg border">
        <div className="grid w-full" style={{ gridTemplateColumns: `4.5rem repeat(${columnCount}, minmax(13rem, 1fr))` }}>
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="bg-card sticky top-0 left-0 z-40 flex flex-col items-center justify-center gap-0.5 border-r border-b px-1 py-2.5">
                <span className="text-muted-foreground truncate text-[9px] leading-none">Expediente</span>
                <span className="text-[10px] leading-none font-medium tabular-nums">00:00</span>
                <span className="text-muted-foreground text-[9px] leading-none">a</span>
                <span className="text-[10px] leading-none font-medium tabular-nums">23:59</span>
              </div>
            </TooltipTrigger>
            <TooltipContent side="right">
              {workingHoursLabel
                ? `Grade cobre o dia inteiro — expediente configurado: ${formatHourLabel(workingHoursLabel.start, 0)} - ${formatHourLabel(workingHoursLabel.end, 0)}`
                : "Grade cobre o dia inteiro — nenhum horario de funcionamento configurado."}
            </TooltipContent>
          </Tooltip>
          {columns.map((column) =>
            column.resource ? (
              <div key={column.key} className="bg-card sticky top-0 z-30 border-r border-b last:border-r-0">
                <ResourceColumnHeader
                  resource={column.resource}
                  availability={computeResourceAvailability(column.resourceId)}
                  appointmentCount={
                    visibleAppointments.filter(
                      (a) =>
                        a.resourceId === column.resourceId &&
                        startOfDay(new Date(a.startUtc)).getTime() === startOfDay(column.date).getTime()
                    ).length
                  }
                  utilizationPercent={(() => {
                    const windows = (workingHoursByResourceId.get(column.resourceId) ?? []).filter(
                      (w) => w.dayOfWeek === column.date.toLocaleDateString("en-US", { weekday: "long" })
                    );
                    if (windows.length === 0) {
                      return null;
                    }
                    const workingMinutes = windows.reduce((sum, w) => {
                      const [sh, sm] = w.startTime.split(":").map(Number);
                      const [eh, em] = w.endTime.split(":").map(Number);
                      return sum + (eh * 60 + em - (sh * 60 + sm));
                    }, 0);
                    const bookedMinutes = (appointmentsQuery.data ?? [])
                      .filter(
                        (a) =>
                          a.resourceId === column.resourceId &&
                          startOfDay(new Date(a.startUtc)).getTime() === startOfDay(column.date).getTime() &&
                          a.status !== "CancelledByCustomer" &&
                          a.status !== "CancelledByStaff"
                      )
                      .reduce((sum, a) => sum + (new Date(a.endUtc).getTime() - new Date(a.startUtc).getTime()) / 60000, 0);
                    return workingMinutes > 0 ? (bookedMinutes / workingMinutes) * 100 : null;
                  })()}
                />
              </div>
            ) : (
              <div key={column.key} className="text-muted-foreground border-r border-b p-3 text-center text-xs font-medium capitalize last:border-r-0">
                {column.label}
              </div>
            )
          )}
          {options.showAddResourceColumn && (
            <div className="bg-card sticky top-0 z-30 flex items-center justify-center border-b p-3">
              <Button asChild variant="ghost" size="sm" className="border-border/70 text-muted-foreground h-auto border border-dashed py-2">
                <Link href="/recursos">
                  <Plus className="size-3.5" />
                  Adicionar profissional
                </Link>
              </Button>
            </div>
          )}

          <div className="bg-card sticky left-0 z-20 border-r" style={{ height: gridHeightPx }}>
            {Array.from({ length: slotCount }).map((_, index) => {
              const totalMinutesFromStart = DAY_START_HOUR * 60 + index * SLOT_MINUTES;
              return (
                <div
                  key={index}
                  // O primeiro rotulo nao pode subir metade da altura: ficaria
                  // por cima da borda do cabecalho (fora da area do grid).
                  className={`text-muted-foreground absolute right-2 text-[10px] tabular-nums ${index === 0 ? "" : "-translate-y-1/2"} ${index % 2 === 0 ? "font-medium" : "opacity-60"}`}
                  style={{ top: index * rowHeightPx + (index === 0 ? 2 : 0) }}
                >
                  {formatHourLabel(Math.floor(totalMinutesFromStart / 60), totalMinutesFromStart % 60)}
                </div>
              );
            })}
          </div>

          {columns.map((column) => {
            const bands = computeBlockedBands(column.resourceId, column.date, rowHeightPx);
            const isToday = startOfDay(column.date).getTime() === startOfDay(now).getTime();
            const nowTop = (minutesFromDayStart(now, DAY_START_HOUR) / SLOT_MINUTES) * rowHeightPx;
            const showNowLine = isToday && nowTop >= 0 && nowTop <= gridHeightPx;

            return (
              <div key={column.key} className="relative border-r last:border-r-0" style={{ height: gridHeightPx }}>
                {bands.map((band, index) => (
                  <BreakBand key={index} band={band} />
                ))}

                {Array.from({ length: slotCount }).map((_, index) => {
                  const slotStart = new Date(column.date);
                  slotStart.setHours(DAY_START_HOUR, 0, 0, 0);
                  slotStart.setMinutes(slotStart.getMinutes() + index * SLOT_MINUTES);
                  const top = index * rowHeightPx;
                  const blocked = isMinuteBlocked(bands, top);
                  return (
                    <button
                      key={index}
                      type="button"
                      disabled={blocked}
                      aria-label={`Novo agendamento em ${column.label} as ${slotStart.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}`}
                      className={`hover:enabled:bg-primary/5 absolute inset-x-0 disabled:cursor-not-allowed ${index % 2 === 0 ? "border-t" : "border-border/40 border-t border-dashed"} first:border-t-0`}
                      style={{ top, height: rowHeightPx }}
                      onClick={() => openCreateDialog(column.resourceId, slotStart)}
                      onDoubleClick={() => openCreateDialog(column.resourceId, slotStart)}
                      onDragOver={(event) => event.preventDefault()}
                      onDrop={(event) => {
                        event.preventDefault();
                        handleDropOnSlot(column.resourceId, slotStart);
                      }}
                    />
                  );
                })}

                {/* Linha do horario atual — so na coluna de hoje, atravessando
                    toda a largura dela. Rotulo so na primeira coluna pra nao
                    repetir a mesma etiqueta N vezes lado a lado. */}
                {showNowLine && (
                  <div className="pointer-events-none absolute inset-x-0 z-20 flex items-center" style={{ top: nowTop }}>
                    <span className="bg-destructive size-1.5 shrink-0 rounded-full" aria-hidden="true" />
                    <span className="bg-destructive h-px flex-1" aria-hidden="true" />
                  </div>
                )}

                {visibleAppointments
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
                    const top = (minutesFromDayStart(start, DAY_START_HOUR) / SLOT_MINUTES) * rowHeightPx;
                    const height = Math.max(((end.getTime() - start.getTime()) / 60000 / SLOT_MINUTES) * rowHeightPx, MIN_CHIP_HEIGHT_PX);

                    return (
                      <AppointmentChip
                        key={appointment.id}
                        appointment={appointment}
                        customerName={customerNameById.get(appointment.customerId) ?? "Cliente"}
                        draggable={!isCoarsePointer && RESCHEDULABLE_STATUSES.includes(appointment.status)}
                        selected={selectedIds.has(appointment.id)}
                        selectionMode={selectionMode}
                        onSelectToggle={toggleSelection}
                        onOpenDetail={openDetailDialog}
                        onDragStart={() => setDraggingId(appointment.id)}
                        onDragEnd={() => setDraggingId(null)}
                        onQuickStatusAction={handleQuickStatusAction}
                        onResizeCommit={handleResizeCommit}
                        style={{ top, height }}
                        rowHeightPx={rowHeightPx}
                        slotMinutes={SLOT_MINUTES}
                        density={density}
                      />
                    );
                  })}
              </div>
            );
          })}

          {options.showAddResourceColumn && <div className="bg-muted/20" style={{ height: gridHeightPx }} />}
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
              aria-label={`${day.toLocaleDateString("pt-BR", { weekday: "long", day: "2-digit", month: "long", year: "numeric" })}${
                dayAppointments.length > 0 ? `, ${dayAppointments.length} agendamento(s)` : ""
              }`}
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

  const dayColumns = activeResources.map((resource) => ({
    key: resource.id,
    label: resource.name,
    resourceId: resource.id,
    date: currentDate,
    resource,
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

  const isLoadingGrid = resourcesQuery.isLoading || appointmentsQuery.isLoading;
  const hasError = resourcesQuery.isError || appointmentsQuery.isError;

  return (
    // Margem negativa pra recuperar parte do padding generoso do layout
    // (p-6 sm:p-10, calibrado pra telas de formulario): a Agenda e a tela mais
    // densa do app — cada pixel horizontal vira largura de coluna de
    // profissional. O layout continua igual pras outras telas.
    <div className="-mx-2 flex w-[calc(100%+1rem)] flex-1 flex-col gap-4 overflow-x-hidden sm:-mx-6 sm:w-[calc(100%+3rem)]">
      {/* Navegacao de data a esquerda, acoes a direita — mesma hierarquia da
          referencia: primeiro "que dia estou vendo", depois "o que faco". */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
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
          <div className="flex items-center gap-2">
            <CalendarDays className="text-muted-foreground size-4" aria-hidden="true" />
            <span className="text-base font-semibold capitalize">{rangeLabel}</span>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div className="relative">
            <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" />
            <Input
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
              placeholder="Buscar cliente, servico..."
              className="w-56 pl-8"
            />
          </div>
          <AgendaFiltersPopover
            filters={filters}
            onChange={setFilters}
            services={servicesQuery.data?.items ?? []}
            resources={activeResources}
          />
          <Button
            size="sm"
            onClick={() => {
              if (resolvedResourceId) {
                openCreateDialog(resolvedResourceId, nextAvailableSlot(DAY_START_HOUR, DAY_END_HOUR));
              }
            }}
            disabled={!resolvedResourceId}
          >
            <Plus className="size-4" />
            Novo agendamento
          </Button>
        </div>
      </div>

      {!isFiltersEmpty(filters) && (
        <div className="flex flex-wrap items-center gap-1.5">
          {Array.from(filters.statuses).map((status) => (
            <Badge key={status} variant="secondary" className="gap-1">
              {APPOINTMENT_STATUS_LABELS[status]}
              <button type="button" onClick={() => setFilters({ ...filters, statuses: new Set([...filters.statuses].filter((s) => s !== status)) })}>
                <X className="size-3" />
              </button>
            </Badge>
          ))}
          {Array.from(filters.serviceIds).map((id) => (
            <Badge key={id} variant="secondary" className="gap-1">
              {servicesQuery.data?.items.find((s) => s.id === id)?.name ?? "Servico"}
              <button type="button" onClick={() => setFilters({ ...filters, serviceIds: new Set([...filters.serviceIds].filter((s) => s !== id)) })}>
                <X className="size-3" />
              </button>
            </Badge>
          ))}
          {Array.from(filters.resourceIds).map((id) => (
            <Badge key={id} variant="secondary" className="gap-1">
              {resourceNameById.get(id) ?? "Profissional"}
              <button
                type="button"
                onClick={() => setFilters({ ...filters, resourceIds: new Set([...filters.resourceIds].filter((r) => r !== id)) })}
              >
                <X className="size-3" />
              </button>
            </Badge>
          ))}
          <Button variant="ghost" size="sm" onClick={() => setFilters({ statuses: new Set(), serviceIds: new Set(), resourceIds: new Set() })}>
            Limpar filtros
          </Button>
        </div>
      )}

      <div className="grid grid-cols-1 gap-3 xl:grid-cols-[minmax(0,1fr)_18rem]">
        <AgendaKpiRow appointmentsToday={todayAppointmentsQuery.data} isLoading={todayAppointmentsQuery.isLoading} />
        <NextAppointmentCard
          appointmentsToday={todayAppointmentsQuery.data}
          customerNameById={customerNameById}
          resourceNameById={resourceNameById}
          isLoading={todayAppointmentsQuery.isLoading}
          onOpenDetail={openDetailDialog}
        />
      </div>

      {selectedIds.size > 0 && (
        <div className="bg-card flex items-center justify-between gap-3 rounded-lg border p-3 text-sm shadow-sm">
          <span>{selectedIds.size} selecionado(s)</span>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" onClick={() => handleBulkAction("confirm")}>
              Confirmar selecionados
            </Button>
            <Button size="sm" variant="destructive" onClick={() => handleBulkAction("cancel")}>
              Cancelar selecionados
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setSelectedIds(new Set())}>
              Limpar selecao
            </Button>
          </div>
        </div>
      )}

      <Card className="min-w-0 py-4">
        <CardContent className="flex min-w-0 flex-col gap-4 px-4">
          <Tabs value={view} onValueChange={(value) => setView(value as typeof view)} className="min-w-0">
            <div className="flex flex-wrap items-center justify-end gap-2">
              {showUnitFilter && (
                <Select
                  value={selectedUnitId ?? undefined}
                  onValueChange={(value) => {
                    setSelectedUnitId(value);
                    setSelectedResourceId(null);
                  }}
                >
                  <SelectTrigger size="sm" className="w-40" aria-label="Filtrar por unidade">
                    <SelectValue placeholder="Todas as unidades" />
                  </SelectTrigger>
                  <SelectContent>
                    {(unitsQuery.data ?? []).map((unit) => (
                      <SelectItem key={unit.id} value={unit.id}>
                        {unit.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
              {view === "week" && (
                <Select value={resolvedResourceId ?? undefined} onValueChange={setSelectedResourceId}>
                  <SelectTrigger size="sm" className="w-40" aria-label="Filtrar por recurso">
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
              <TabsList>
                <TabsTrigger value="day">Dia</TabsTrigger>
                <TabsTrigger value="week">Semana</TabsTrigger>
                <TabsTrigger value="month">Mes</TabsTrigger>
              </TabsList>
            </div>

            {isLoadingGrid ? (
              view === "month" ? (
                <div className="grid grid-cols-7 gap-2">
                  {Array.from({ length: 42 }).map((_, index) => (
                    <Skeleton key={index} className="min-h-24 rounded-lg" />
                  ))}
                </div>
              ) : (
                <Skeleton className="h-[500px] rounded-lg" />
              )
            ) : hasError ? (
              <div className="border-destructive/50 bg-destructive/5 rounded-lg border p-4 text-sm">
                <p className="text-destructive font-medium">Nao foi possivel carregar a agenda agora.</p>
                <p className="text-muted-foreground mt-1">Tente novamente em instantes — pode ser uma falha temporaria de rede.</p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="mt-3"
                  onClick={() => {
                    resourcesQuery.refetch();
                    appointmentsQuery.refetch();
                  }}
                >
                  Tentar novamente
                </Button>
              </div>
            ) : activeResources.length === 0 ? (
              <EmptyState
                icon={Users}
                title="Nenhum recurso ativo"
                description="Cadastre ao menos um recurso ativo para comecar a agendar."
                action={
                  <Button asChild size="sm">
                    <Link href="/recursos">Ir para Recursos</Link>
                  </Button>
                }
              />
            ) : (
              <>
                <TabsContent value="day" className="min-w-0">
                  {renderTimeGrid(dayColumns, { showAddResourceColumn: true })}
                </TabsContent>
                <TabsContent value="week" className="min-w-0">
                  {renderTimeGrid(weekColumns)}
                </TabsContent>
                <TabsContent value="month">{renderMonthView()}</TabsContent>
              </>
            )}
          </Tabs>

          {!isLoadingGrid && !hasError && activeResources.length > 0 && view !== "month" && (
            <AgendaToolbarFooter rowHeightPx={rowHeightPx} onRowHeightChange={setRowHeightPx} density={density} onDensityChange={setDensity} />
          )}
        </CardContent>
      </Card>

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
                    <Select
                      value={field.value}
                      onValueChange={(value) => (value === NEW_CUSTOMER_VALUE ? setQuickCustomerOpen(true) : field.onChange(value))}
                    >
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecione um cliente" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value={NEW_CUSTOMER_VALUE} className="text-primary font-medium">
                          + Novo cliente
                        </SelectItem>
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

      <Dialog open={quickCustomerOpen} onOpenChange={setQuickCustomerOpen}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Novo cliente</DialogTitle>
          </DialogHeader>
          <form
            className="flex flex-col gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              if (!quickCustomerName.trim()) {
                toast.error("Informe o nome do cliente.");
                return;
              }
              quickCreateCustomerMutation.mutate();
            }}
          >
            <div>
              <label htmlFor="quick-customer-name" className="mb-1 block text-sm font-medium">
                Nome completo
              </label>
              <Input
                id="quick-customer-name"
                value={quickCustomerName}
                onChange={(event) => setQuickCustomerName(event.target.value)}
                autoFocus
              />
            </div>
            <div>
              <label htmlFor="quick-customer-phone" className="mb-1 block text-sm font-medium">
                Telefone (opcional)
              </label>
              <Input
                id="quick-customer-phone"
                value={quickCustomerPhone}
                onChange={(event) => setQuickCustomerPhone(event.target.value)}
              />
            </div>
            <p className="text-muted-foreground text-xs">
              Cadastro rapido — o perfil completo (e-mail, CPF, observacoes) pode ser preenchido depois em Clientes.
            </p>
            <DialogFooter>
              <Button type="submit" disabled={quickCreateCustomerMutation.isPending}>
                {quickCreateCustomerMutation.isPending ? "Cadastrando..." : "Cadastrar e selecionar"}
              </Button>
            </DialogFooter>
          </form>
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
                      rescheduleMutation.mutate({
                        id: selectedAppointment.id,
                        newStartAtUtc: new Date(values.newStartAtLocal).toISOString(),
                        reason: values.reason.trim() === "" ? null : values.reason.trim(),
                        newResourceId:
                          reassignResourceId && reassignResourceId !== selectedAppointment.resourceId ? reassignResourceId : undefined,
                      })
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
                    {activeResources.length > 1 && (
                      <FormItem>
                        <FormLabel>Profissional</FormLabel>
                        <Select
                          value={reassignResourceId ?? selectedAppointment.resourceId}
                          onValueChange={setReassignResourceId}
                        >
                          <FormControl>
                            <SelectTrigger className="w-full">
                              <SelectValue />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            {activeResources.map((resource) => (
                              <SelectItem key={resource.id} value={resource.id}>
                                {resource.name}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FormItem>
                    )}
                    <FormField
                      control={rescheduleForm.control}
                      name="reason"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Motivo (opcional)</FormLabel>
                          <FormControl>
                            <Textarea rows={2} {...field} />
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
              ) : cancelingOpen ? (
                <Form {...cancelForm}>
                  <form
                    onSubmit={cancelForm.handleSubmit((values) =>
                      cancelMutation.mutate({ id: selectedAppointment.id, reason: values.reason.trim() === "" ? null : values.reason.trim() })
                    )}
                    className="flex flex-col gap-3"
                  >
                    <FormField
                      control={cancelForm.control}
                      name="reason"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Motivo do cancelamento (opcional)</FormLabel>
                          <FormControl>
                            <Textarea rows={2} {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <div className="flex gap-2">
                      <Button type="submit" size="sm" variant="destructive" disabled={cancelMutation.isPending}>
                        {cancelMutation.isPending ? "Cancelando..." : "Confirmar cancelamento"}
                      </Button>
                      <Button type="button" variant="ghost" size="sm" onClick={() => setCancelingOpen(false)}>
                        Voltar
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
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          rescheduleForm.reset({ newStartAtLocal: "", reason: "" });
                          setReassignResourceId(null);
                          setReschedulingOpen(true);
                        }}
                      >
                        Remarcar
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => noShowMutation.mutate(selectedAppointment.id)}>
                        Nao compareceu
                      </Button>
                      <Button
                        size="sm"
                        variant="destructive"
                        onClick={() => {
                          cancelForm.reset({ reason: "" });
                          setCancelingOpen(true);
                        }}
                      >
                        Cancelar
                      </Button>
                    </>
                  )}
                </DialogFooter>
              )}

              {!reschedulingOpen && !cancelingOpen && depositQuery.data && (
                <div className="flex flex-col gap-2 border-t pt-3">
                  <h3 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">Sinal</h3>
                  <div className="flex items-center gap-2 text-xs">
                    <Badge
                      variant={
                        depositQuery.data.status === "Paid" ? "success" : depositQuery.data.status === "Failed" ? "destructive" : "secondary"
                      }
                    >
                      {depositQuery.data.status === "Paid" ? "Pago" : depositQuery.data.status === "Failed" ? "Falhou" : "Pendente"}
                    </Badge>
                    <span>
                      {new Intl.NumberFormat("pt-BR", { style: "currency", currency: depositQuery.data.currency }).format(
                        depositQuery.data.amount
                      )}
                    </span>
                    {depositQuery.data.invoiceUrl && depositQuery.data.status === "Pending" && (
                      <a href={depositQuery.data.invoiceUrl} target="_blank" rel="noopener noreferrer" className="text-primary underline">
                        Ver link de pagamento
                      </a>
                    )}
                  </div>
                </div>
              )}

              {!reschedulingOpen && !cancelingOpen && (changeLogQuery.data?.items.length ?? 0) > 0 && (
                <div className="flex flex-col gap-2 border-t pt-3">
                  <h3 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">Historico</h3>
                  <ul className="flex flex-col gap-2 text-xs">
                    {changeLogQuery.data!.items.map((entry) => (
                      <li key={entry.id} className="border-border/60 border-l-2 pl-2">
                        <p>
                          <span className="font-medium">{entry.changeType === "Cancelled" ? "Cancelado" : "Remarcado"}</span>{" "}
                          <span className="text-muted-foreground">em {formatChangeLogDateTime(entry.occurredAtUtc)}</span>
                        </p>
                        {entry.changeType === "Rescheduled" && entry.newStartUtc && (
                          <p className="text-muted-foreground">
                            {formatChangeLogDateTime(entry.previousStartUtc)} {"->"} {formatChangeLogDateTime(entry.newStartUtc)}
                          </p>
                        )}
                        {entry.previousResourceId && (
                          <p className="text-muted-foreground">
                            Profissional: {entry.previousResourceName ?? "—"} {"->"} {entry.resourceName}
                          </p>
                        )}
                        {entry.newEndUtc && (
                          <p className="text-muted-foreground">
                            Duracao ate {new Date(entry.newEndUtc).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}
                          </p>
                        )}
                        {entry.reason && <p className="italic">&ldquo;{entry.reason}&rdquo;</p>}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
