import * as React from "react";
import { CalendarClock } from "lucide-react";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { AppointmentSummary } from "@/lib/api/client";

function initials(name: string): string {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

const NON_TERMINAL_STATUSES: AppointmentSummary["status"][] = ["Scheduled", "Confirmed"];

export function NextAppointmentCard({
  appointmentsToday,
  customerNameById,
  resourceNameById,
  isLoading,
  onOpenDetail,
}: {
  appointmentsToday: AppointmentSummary[] | undefined;
  customerNameById: Map<string, string>;
  resourceNameById?: Map<string, string>;
  isLoading: boolean;
  onOpenDetail?: (id: string) => void;
}) {
  const next = React.useMemo(() => {
    const now = new Date().getTime();
    return (appointmentsToday ?? [])
      .filter((a) => NON_TERMINAL_STATUSES.includes(a.status) && new Date(a.startUtc).getTime() >= now)
      .sort((a, b) => a.startUtc.localeCompare(b.startUtc))[0];
  }, [appointmentsToday]);

  const customerName = next ? (customerNameById.get(next.customerId) ?? "Cliente") : "";

  return (
    <Card className="py-0">
      <CardContent className="flex flex-col gap-2 px-3 py-3">
        <span className="text-muted-foreground text-xs">Proximo atendimento</span>

        {isLoading ? (
          <Skeleton className="h-12 w-full" />
        ) : next ? (
          <button
            type="button"
            onClick={() => onOpenDetail?.(next.id)}
            className="hover:bg-muted/50 -mx-1 flex items-center gap-2.5 rounded-md px-1 py-1 text-left transition-colors"
          >
            <Avatar size="lg" className="shrink-0">
              <AvatarFallback className="text-xs">{initials(customerName)}</AvatarFallback>
            </Avatar>
            <div className="min-w-0">
              <p className="text-sm font-semibold tabular-nums">{formatTime(next.startUtc)}</p>
              <p className="truncate text-xs font-medium">{customerName}</p>
              <p className="text-muted-foreground truncate text-[11px]">
                {next.serviceName}
                {resourceNameById?.get(next.resourceId) ? ` · ${resourceNameById.get(next.resourceId)}` : ""}
              </p>
            </div>
          </button>
        ) : (
          <div className="text-muted-foreground flex items-center gap-2 py-2 text-xs">
            <CalendarClock className="size-4 shrink-0" aria-hidden="true" />
            <span>Nenhum atendimento pendente hoje.</span>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
