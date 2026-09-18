"use client";

import Link from "next/link";
import { Sparkles } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { APPOINTMENT_STATUS_LABELS, APPOINTMENT_STATUS_VARIANTS } from "@/lib/appointment-status";
import type { AppointmentStatus } from "@/lib/api/client";
import { cn } from "@/lib/utils";

export type TodayAppointmentItem = {
  id: string;
  startUtc: string;
  customerName: string;
  serviceName: string;
  price: number;
  currency: string;
  status: AppointmentStatus;
};

// Verde = concluido, vermelho = no-show/cancelado, azul = confirmado, o resto
// neutro -- so o "ponto" de status ao lado do horario, escaneavel sem ler o texto.
const STATUS_DOT_CLASSES: Record<AppointmentStatus, string> = {
  Scheduled: "bg-muted-foreground",
  Confirmed: "bg-info",
  InProgress: "bg-primary",
  Completed: "bg-success",
  NoShow: "bg-destructive",
  CancelledByCustomer: "bg-destructive",
  CancelledByStaff: "bg-destructive",
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
  const total = appointments?.length ?? 0;
  const completed = appointments?.filter((a) => a.status === "Completed").length ?? 0;
  const awaiting = appointments?.filter((a) => a.status === "Scheduled").length ?? 0;

  const isEmpty = !isLoading && (!appointments || appointments.length === 0);

  return (
    // border-l mais grosso na cor primaria: unico bloco do painel com esse
    // acento -- da a "hierarquia visual um pouco maior" pedida sem colorir o
    // card inteiro nem usar sombra pesada.
    <Card className="border-border/70 ring-0 shadow-none border-l-2 border-l-primary/60">
      <CardContent>
        <div className={cn("flex flex-wrap items-center justify-between gap-3", isEmpty ? "mb-3" : "mb-4")}>
          <div>
            <h3 className="text-base font-semibold">Agenda de hoje</h3>
            <p className="text-muted-foreground text-xs">Seus proximos atendimentos</p>
          </div>
          <div className="flex items-center gap-3">
            {!isLoading && total > 0 && (
              <p className="text-muted-foreground hidden text-xs sm:block">
                <span className="text-foreground font-medium tabular-nums">{total}</span> hoje ·{" "}
                <span className="text-foreground font-medium tabular-nums">{completed}</span> concluidos ·{" "}
                <span className="text-foreground font-medium tabular-nums">{awaiting}</span> aguardando
              </p>
            )}
            <Button asChild variant="ghost" size="sm">
              <Link href="/agenda">Ver agenda completa →</Link>
            </Button>
          </div>
        </div>

        {isLoading ? (
          <div className="flex flex-col gap-3">
            {[0, 1, 2].map((key) => (
              <Skeleton key={key} className="h-14 w-full" />
            ))}
          </div>
        ) : !appointments || appointments.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-2 py-5 text-center">
            <div className="bg-primary/10 flex size-9 items-center justify-center rounded-full">
              <Sparkles className="text-primary size-4" aria-hidden="true" />
            </div>
            <div className="space-y-0.5">
              <p className="text-sm font-medium">Sua agenda esta livre hoje</p>
              <p className="text-muted-foreground text-xs">Voce ainda nao possui atendimentos agendados.</p>
            </div>
            <Button asChild size="sm" className="mt-1">
              <Link href="/agenda?novo=1">+ Criar primeiro agendamento</Link>
            </Button>
          </div>
        ) : (
          <ul className="flex flex-col divide-y">
            {appointments.map((appointment) => (
              <li
                key={appointment.id}
                className="hover:bg-card-hover -mx-2 flex items-center gap-3 rounded-lg px-2 py-3 transition-colors first:pt-0 last:pb-0"
              >
                <span
                  aria-hidden="true"
                  className={cn("size-2 shrink-0 rounded-full", STATUS_DOT_CLASSES[appointment.status])}
                />
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
      </CardContent>
    </Card>
  );
}
