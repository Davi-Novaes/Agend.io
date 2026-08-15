"use client";

import * as React from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { CalendarDays, Gift, Mail, MessageCircle, Scissors, User, Wallet } from "lucide-react";

import {
  getCustomerById,
  getCustomerAppointmentHistory,
  getTenantProfile,
  listNotificationHistory,
  redeemCustomerLoyaltyReward,
  ApiError,
  type AppointmentStatus,
  type CustomerAppointmentHistoryItem,
  type NotificationLogItem,
} from "@/lib/api/client";
import { useSession } from "@/lib/auth/session-context";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
} from "@/components/ui/alert-dialog";

const STATUS_LABELS: Record<AppointmentStatus, string> = {
  Scheduled: "Agendado",
  Confirmed: "Confirmado",
  InProgress: "Em andamento",
  Completed: "Concluido",
  NoShow: "Nao compareceu",
  CancelledByCustomer: "Cancelado (cliente)",
  CancelledByStaff: "Cancelado (equipe)",
};

const STATUS_VARIANTS: Record<AppointmentStatus, "secondary" | "info" | "default" | "success" | "destructive"> = {
  Scheduled: "secondary",
  Confirmed: "info",
  InProgress: "default",
  Completed: "success",
  NoShow: "destructive",
  CancelledByCustomer: "destructive",
  CancelledByStaff: "destructive",
};

const CHANNEL_LABEL: Record<NotificationLogItem["channel"], string> = {
  Email: "E-mail",
  WhatsApp: "WhatsApp",
};

const TRIGGER_LABEL: Record<NotificationLogItem["trigger"], string> = {
  Scheduled: "Agendamento confirmado",
  Reminder: "Lembrete",
  Cancelled: "Cancelamento",
  Rescheduled: "Reagendamento",
  Confirmed: "Confirmação de presença",
  Completed: "Pós-atendimento",
};

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString("pt-BR");
}

function formatCurrency(amount: number, currency: string): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency }).format(amount);
}

type TimelineEntry =
  | { kind: "appointment"; timestamp: string; item: CustomerAppointmentHistoryItem }
  | { kind: "notification"; timestamp: string; item: NotificationLogItem };

export function CustomerProfileSheet({
  customerId,
  onOpenChange,
}: {
  customerId: string | null;
  onOpenChange: (open: boolean) => void;
}) {
  const { session } = useSession();
  const accessToken = session?.accessToken ?? "";

  return (
    <Sheet open={Boolean(customerId)} onOpenChange={onOpenChange}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-lg">
        {customerId ? <CustomerProfileContent customerId={customerId} accessToken={accessToken} /> : null}
      </SheetContent>
    </Sheet>
  );
}

function CustomerProfileContent({
  customerId,
  accessToken,
}: {
  customerId: string;
  accessToken: string;
}) {
  const customerQuery = useQuery({
    queryKey: ["customers", "detail", customerId],
    queryFn: () => getCustomerById(customerId, accessToken),
    enabled: Boolean(accessToken),
  });

  const historyQuery = useQuery({
    queryKey: ["customers", "history", customerId],
    queryFn: () => getCustomerAppointmentHistory(customerId, accessToken),
    enabled: Boolean(accessToken),
  });

  const notificationsQuery = useQuery({
    queryKey: ["customers", "notifications", customerId],
    queryFn: () => listNotificationHistory({ page: 1, pageSize: 50, customerId }, accessToken),
    enabled: Boolean(accessToken),
  });

  const tenantProfileQuery = useQuery({
    queryKey: ["tenant", "profile"],
    queryFn: () => getTenantProfile(accessToken),
    enabled: Boolean(accessToken),
  });

  const isLoading = customerQuery.isLoading || historyQuery.isLoading || notificationsQuery.isLoading;

  const timeline: TimelineEntry[] = React.useMemo(() => {
    const appointmentEntries: TimelineEntry[] = (historyQuery.data?.items ?? []).map((item) => ({
      kind: "appointment",
      timestamp: item.startUtc,
      item,
    }));
    const notificationEntries: TimelineEntry[] = (notificationsQuery.data?.items ?? []).map((item) => ({
      kind: "notification",
      timestamp: item.sentAtUtc,
      item,
    }));
    return [...appointmentEntries, ...notificationEntries].sort(
      (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    );
  }, [historyQuery.data, notificationsQuery.data]);

  if (isLoading || !customerQuery.data) {
    return (
      <div className="flex flex-col gap-4 p-4">
        <Skeleton className="h-6 w-40" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const customer = customerQuery.data;
  const history = historyQuery.data;

  return (
    <div className="flex flex-col gap-6 overflow-y-auto p-4">
      <SheetHeader className="p-0">
        <SheetTitle className="flex items-center gap-2">
          {customer.fullName}
          <Badge variant={customer.isActive ? "default" : "outline"}>{customer.isActive ? "Ativo" : "Inativo"}</Badge>
        </SheetTitle>
        <SheetDescription className="flex flex-col gap-0.5">
          {customer.email ? <span>{customer.email}</span> : null}
          {customer.phone ? <span>{customer.phone}</span> : null}
        </SheetDescription>
      </SheetHeader>

      <div className="grid grid-cols-2 gap-3">
        <StatBlock icon={CalendarDays} label="Total de visitas" value={String(history?.totalVisits ?? 0)} />
        <StatBlock
          icon={Wallet}
          label="Total gasto"
          value={history && history.totalVisits > 0 ? formatCurrency(history.totalSpent, history.totalSpentCurrency ?? "BRL") : "—"}
        />
        <StatBlock
          icon={CalendarDays}
          label="Última visita"
          value={history?.lastVisitAtUtc ? formatDateTime(history.lastVisitAtUtc) : "—"}
        />
        <StatBlock
          icon={CalendarDays}
          label="Próximo agendamento"
          value={history?.nextAppointmentAtUtc ? formatDateTime(history.nextAppointmentAtUtc) : "—"}
        />
      </div>

      {history?.favoriteServiceName || history?.favoriteProfessionalName ? (
        <div className="flex flex-col gap-1 text-sm">
          {history.favoriteServiceName ? (
            <div className="flex items-center gap-2 text-muted-foreground">
              <Scissors className="size-4" />
              Serviço preferido: <span className="font-medium text-foreground">{history.favoriteServiceName}</span>
            </div>
          ) : null}
          {history.favoriteProfessionalName ? (
            <div className="flex items-center gap-2 text-muted-foreground">
              <User className="size-4" />
              Profissional preferido: <span className="font-medium text-foreground">{history.favoriteProfessionalName}</span>
            </div>
          ) : null}
        </div>
      ) : null}

      {tenantProfileQuery.data?.loyaltyProgramEnabled ? (
        <LoyaltySection
          customerId={customerId}
          accessToken={accessToken}
          loyaltyPoints={customer.loyaltyPoints}
          loyaltyVisitsForReward={tenantProfileQuery.data.loyaltyVisitsForReward}
          loyaltyRewardDescription={tenantProfileQuery.data.loyaltyRewardDescription}
        />
      ) : null}

      {customer.notes ? (
        <div className="flex flex-col gap-1">
          <p className="text-sm font-medium">Observações</p>
          <p className="text-sm text-muted-foreground">{customer.notes}</p>
        </div>
      ) : null}

      <div className="flex flex-col gap-3">
        <p className="text-sm font-medium">Histórico</p>
        {timeline.length === 0 ? (
          <EmptyState
            icon={CalendarDays}
            title="Nenhum evento ainda"
            description="Agendamentos e mensagens enviadas a este cliente aparecem aqui."
          />
        ) : (
          <ol className="flex flex-col gap-4 border-l border-border pl-4">
            {timeline.map((entry) => (
              <li key={`${entry.kind}-${entry.kind === "appointment" ? entry.item.appointmentId : entry.item.id}`} className="relative">
                <span className="absolute top-1.5 -left-[21px] size-2.5 rounded-full bg-primary" />
                {entry.kind === "appointment" ? (
                  <div className="flex flex-col gap-1">
                    <div className="flex items-center gap-2">
                      <Badge variant={STATUS_VARIANTS[entry.item.status]}>{STATUS_LABELS[entry.item.status]}</Badge>
                      <span className="text-xs text-muted-foreground">{formatDateTime(entry.item.startUtc)}</span>
                    </div>
                    <p className="text-sm">
                      {entry.item.serviceName} com {entry.item.professionalName} —{" "}
                      {formatCurrency(entry.item.price, entry.item.currency)}
                    </p>
                  </div>
                ) : (
                  <div className="flex flex-col gap-1">
                    <div className="flex items-center gap-2">
                      {entry.item.channel === "Email" ? <Mail className="size-3.5" /> : <MessageCircle className="size-3.5" />}
                      <span className="text-xs text-muted-foreground">{formatDateTime(entry.item.sentAtUtc)}</span>
                    </div>
                    <p className="text-sm">
                      {TRIGGER_LABEL[entry.item.trigger]} por {CHANNEL_LABEL[entry.item.channel]}
                      {entry.item.status === "Failed" ? " — falhou" : ""}
                    </p>
                  </div>
                )}
              </li>
            ))}
          </ol>
        )}
      </div>
    </div>
  );
}

function LoyaltySection({
  customerId,
  accessToken,
  loyaltyPoints,
  loyaltyVisitsForReward,
  loyaltyRewardDescription,
}: {
  customerId: string;
  accessToken: string;
  loyaltyPoints: number;
  loyaltyVisitsForReward: number;
  loyaltyRewardDescription: string;
}) {
  const queryClient = useQueryClient();
  const [confirmOpen, setConfirmOpen] = React.useState(false);
  const canRedeem = loyaltyPoints >= loyaltyVisitsForReward;

  const redeemMutation = useMutation({
    mutationFn: () => redeemCustomerLoyaltyReward(customerId, accessToken),
    onSuccess: () => {
      toast.success("Recompensa resgatada.");
      queryClient.invalidateQueries({ queryKey: ["customers", "detail", customerId] });
      setConfirmOpen(false);
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Não foi possível resgatar a recompensa."),
  });

  return (
    <>
      <div className="flex flex-col gap-2 rounded-lg border border-border p-3">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 text-sm font-medium">
            <Gift className="size-4" />
            Fidelidade
          </div>
          <span className="text-sm text-muted-foreground">
            {loyaltyPoints} / {loyaltyVisitsForReward} visitas
          </span>
        </div>
        <p className="text-xs text-muted-foreground">Recompensa: {loyaltyRewardDescription}</p>
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="w-fit"
          disabled={!canRedeem}
          onClick={() => setConfirmOpen(true)}
        >
          Resgatar recompensa
        </Button>
      </div>

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Resgatar recompensa?</AlertDialogTitle>
            <AlertDialogDescription>
              Isso debita {loyaltyVisitsForReward} pontos do cliente e não pode ser desfeito.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction disabled={redeemMutation.isPending} onClick={() => redeemMutation.mutate()}>
              {redeemMutation.isPending ? "Resgatando..." : "Confirmar"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

function StatBlock({ icon: Icon, label, value }: { icon: React.ElementType; label: string; value: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg border border-border p-3">
      <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <Icon className="size-3.5" />
        {label}
      </div>
      <p className="text-sm font-medium">{value}</p>
    </div>
  );
}
