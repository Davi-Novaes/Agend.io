"use client";

import Link from "next/link";
import { CalendarDays } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { APPOINTMENT_STATUS_LABELS, APPOINTMENT_STATUS_VARIANTS } from "@/lib/appointment-status";
import type { AppointmentStatus } from "@/lib/api/client";

export type TodayAppointmentItem = {
  id: string;
  startUtc: string;
  customerName: string;
  serviceName: string;
  price: number;
  currency: string;
  status: AppointmentStatus;
};

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatTime(startUtc: string): string {
  return new Date(startUtc).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

export function TodayAgendaCard({
  appointments,
  isLoading,
}: {
  appointments: TodayAppointmentItem[] | undefined;
  isLoading: boolean;
}) {
  return (
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">Agenda de hoje</h3>
          <p className="text-muted-foreground text-xs">Seus proximos atendimentos</p>
        </div>
        <Button asChild variant="ghost" size="sm">
          <Link href="/agenda">Ver agenda completa →</Link>
        </Button>
      </div>

      {isLoading ? (
        <div className="flex flex-col gap-3">
          {[0, 1, 2].map((key) => (
            <Skeleton key={key} className="h-12 w-full" />
          ))}
        </div>
      ) : !appointments || appointments.length === 0 ? (
        <EmptyState
          icon={CalendarDays}
          title="Voce nao possui agendamentos para hoje"
          description="Que tal criar um novo agendamento?"
          action={
            <Button asChild size="sm" variant="outline">
              <Link href="/agenda?novo=1">+ Novo agendamento</Link>
            </Button>
          }
        />
      ) : (
        <ul className="flex flex-col divide-y">
          {appointments.map((appointment) => (
            <li key={appointment.id} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0">
              <span className="w-12 shrink-0 text-sm font-medium tabular-nums">{formatTime(appointment.startUtc)}</span>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{appointment.customerName}</p>
                <p className="text-muted-foreground truncate text-xs">{appointment.serviceName}</p>
              </div>
              <span className="shrink-0 text-sm tabular-nums">{formatCurrency(appointment.price)}</span>
              <Badge variant={APPOINTMENT_STATUS_VARIANTS[appointment.status]} className="shrink-0">
                {APPOINTMENT_STATUS_LABELS[appointment.status]}
              </Badge>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
