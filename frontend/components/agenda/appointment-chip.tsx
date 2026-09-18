"use client";

import * as React from "react";
import { MoreVertical } from "lucide-react";

import { Checkbox } from "@/components/ui/checkbox";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { APPOINTMENT_STATUS_LABELS } from "@/lib/appointment-status";
import type { AppointmentStatus, AppointmentSummary } from "@/lib/api/client";

export type QuickStatusAction = "confirm" | "start" | "complete" | "noshow" | "cancel";

/**
 * Um tom por status, usando os tokens semanticos que ja existem em
 * globals.css (nenhuma cor nova): verde=confirmado, ambar=aguardando,
 * azul=em atendimento, roxo=finalizado, vermelho=cancelado, cinza=faltou.
 * `dot` alimenta tambem a legenda do rodape, pra cor do chip e da legenda
 * nunca sairem de sincronia.
 */
export const CHIP_TONE: Record<AppointmentStatus, { surface: string; badge: string; dot: string }> = {
  Confirmed: {
    surface: "bg-success/10 border-success/30 border-l-success",
    badge: "bg-success/20 text-success",
    dot: "bg-success",
  },
  Scheduled: {
    surface: "bg-warning/10 border-warning/30 border-l-warning",
    badge: "bg-warning/20 text-warning",
    dot: "bg-warning",
  },
  InProgress: {
    surface: "bg-info/10 border-info/30 border-l-info",
    badge: "bg-info/20 text-info",
    dot: "bg-info",
  },
  Completed: {
    surface: "bg-primary/10 border-primary/30 border-l-primary",
    badge: "bg-primary/20 text-primary",
    dot: "bg-primary",
  },
  CancelledByStaff: {
    surface: "bg-destructive/10 border-destructive/30 border-l-destructive",
    badge: "bg-destructive/20 text-destructive",
    dot: "bg-destructive",
  },
  CancelledByCustomer: {
    surface: "bg-destructive/10 border-destructive/30 border-l-destructive",
    badge: "bg-destructive/20 text-destructive",
    dot: "bg-destructive",
  },
  NoShow: {
    surface: "bg-muted/60 border-border border-l-muted-foreground",
    badge: "bg-muted-foreground/20 text-muted-foreground",
    dot: "bg-muted-foreground",
  },
};

const RESCHEDULABLE_STATUSES: AppointmentStatus[] = ["Scheduled", "Confirmed"];

const DENSITY_CLASSES: Record<"compact" | "normal" | "comfortable", string> = {
  compact: "px-1.5 py-0.5 text-[10px]",
  normal: "px-2 py-1 text-[11px]",
  comfortable: "px-2.5 py-1.5 text-xs",
};

/**
 * Tres niveis em vez de "tudo ou so o nome": um card de 30min colapsado pra
 * uma linha unica ficava vazio e feio. Agora ele ainda mostra horario + nome
 * lado a lado, e so o servico e sacrificado quando nao ha altura.
 */
const HEIGHT_FOR_SERVICE_LINE_PX = 62;
const HEIGHT_FOR_STACKED_LAYOUT_PX = 40;

function formatTimeRange(startUtc: string, endUtc: string): string {
  const options: Intl.DateTimeFormatOptions = { hour: "2-digit", minute: "2-digit" };
  return `${new Date(startUtc).toLocaleTimeString("pt-BR", options)} - ${new Date(endUtc).toLocaleTimeString("pt-BR", options)}`;
}

export function AppointmentChip({
  appointment,
  customerName,
  draggable,
  selected,
  selectionMode,
  onSelectToggle,
  onOpenDetail,
  onDragStart,
  onDragEnd,
  onQuickStatusAction,
  onResizeCommit,
  style,
  rowHeightPx,
  slotMinutes,
  density,
}: {
  appointment: AppointmentSummary;
  customerName: string;
  draggable: boolean;
  selected: boolean;
  selectionMode: boolean;
  onSelectToggle: (id: string) => void;
  onOpenDetail: (id: string) => void;
  onDragStart: () => void;
  onDragEnd: () => void;
  onQuickStatusAction: (id: string, action: QuickStatusAction) => void;
  onResizeCommit: (id: string, newDurationMinutes: number) => void;
  style: React.CSSProperties;
  rowHeightPx: number;
  slotMinutes: number;
  density: "compact" | "normal" | "comfortable";
}) {
  const [previewHeightPx, setPreviewHeightPx] = React.useState<number | null>(null);
  const dragStartClientY = React.useRef(0);
  const originalHeightPx = React.useRef(0);
  // draggable ja encapsula !isCoarsePointer && status remarcavel — mesmo criterio pro redimensionar.
  const isResizable = draggable;
  const tone = CHIP_TONE[appointment.status];

  function handleResizePointerDown(event: React.PointerEvent<HTMLDivElement>) {
    event.stopPropagation();
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    dragStartClientY.current = event.clientY;
    originalHeightPx.current = typeof style.height === "number" ? style.height : rowHeightPx;
    setPreviewHeightPx(originalHeightPx.current);
  }

  function handleResizePointerMove(event: React.PointerEvent<HTMLDivElement>) {
    if (previewHeightPx === null) {
      return;
    }
    const deltaY = event.clientY - dragStartClientY.current;
    const snappedRows = Math.max(1, Math.round((originalHeightPx.current + deltaY) / rowHeightPx));
    setPreviewHeightPx(snappedRows * rowHeightPx);
  }

  function handleResizePointerUp(event: React.PointerEvent<HTMLDivElement>) {
    event.currentTarget.releasePointerCapture(event.pointerId);
    if (previewHeightPx !== null) {
      const newDurationMinutes = Math.round(previewHeightPx / rowHeightPx) * slotMinutes;
      const currentDurationMinutes = (new Date(appointment.endUtc).getTime() - new Date(appointment.startUtc).getTime()) / 60000;
      if (newDurationMinutes !== currentDurationMinutes && newDurationMinutes >= slotMinutes) {
        onResizeCommit(appointment.id, newDurationMinutes);
      }
    }
    setPreviewHeightPx(null);
  }

  const effectiveStyle = previewHeightPx !== null ? { ...style, height: previewHeightPx, zIndex: 30 } : style;
  const renderedHeight = previewHeightPx ?? (typeof style.height === "number" ? style.height : rowHeightPx);
  const showServiceLine = renderedHeight >= HEIGHT_FOR_SERVICE_LINE_PX;
  const isStacked = renderedHeight >= HEIGHT_FOR_STACKED_LAYOUT_PX;
  const timeRange = formatTimeRange(appointment.startUtc, appointment.endUtc);
  const statusLabel = APPOINTMENT_STATUS_LABELS[appointment.status];

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        {/* div, nao button — precisa aninhar Checkbox/DropdownMenuTrigger (ambos
            renderizam <button> por baixo do Radix), e button dentro de button
            e HTML invalido (o navegador fecha o de fora, quebrando os cliques). */}
        <div
          role="button"
          tabIndex={0}
          draggable={draggable}
          onDragStart={onDragStart}
          onDragEnd={onDragEnd}
          onClick={(event) => {
            event.stopPropagation();
            onOpenDetail(appointment.id);
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter" || event.key === " ") {
              event.preventDefault();
              onOpenDetail(appointment.id);
            }
          }}
          className={`group absolute inset-x-1 z-10 flex cursor-pointer flex-col gap-0.5 overflow-hidden rounded-md border border-l-4 text-left leading-tight shadow-sm transition-shadow hover:z-20 hover:shadow-md ${DENSITY_CLASSES[density]} ${tone.surface} ${selected ? "ring-primary ring-2" : ""}`}
          style={effectiveStyle}
        >
          {/* Sempre no DOM (nao so em selectionMode) — senao nao haveria como
              INICIAR uma selecao. Visibilidade e so CSS, igual ao menu. */}
          <span
            role="presentation"
            onClick={(event) => event.stopPropagation()}
            className={`absolute top-1 left-1 z-20 transition-opacity ${selectionMode || selected ? "opacity-100" : "opacity-0 group-hover:opacity-100 group-focus-within:opacity-100"}`}
          >
            <Checkbox
              checked={selected}
              onCheckedChange={() => onSelectToggle(appointment.id)}
              aria-label={`Selecionar agendamento de ${customerName}`}
              className="bg-background size-3.5"
            />
          </span>

          {isStacked ? (
            <>
              <div className="flex items-center justify-between gap-1">
                <span className="truncate font-medium tabular-nums opacity-80">{timeRange}</span>
                <span className={`shrink-0 rounded px-1 py-px text-[9px] font-medium whitespace-nowrap ${tone.badge}`}>{statusLabel}</span>
              </div>
              <p className="truncate pr-4 font-semibold">{customerName}</p>
              {showServiceLine && <p className="truncate pr-4 opacity-70">{appointment.serviceName}</p>}
            </>
          ) : (
            // Card curto (30min no zoom minimo): horario e nome na MESMA linha,
            // e o status vira so a cor da borda esquerda — melhor que exibir o
            // nome sozinho num card semivazio.
            <div className="flex min-w-0 items-baseline gap-1.5 pr-4">
              <span className="shrink-0 font-medium tabular-nums opacity-80">{timeRange.slice(0, 5)}</span>
              <span className="truncate font-semibold">{customerName}</span>
            </div>
          )}

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <span
                role="button"
                aria-label="Acoes rapidas"
                onClick={(event) => event.stopPropagation()}
                className="hover:bg-background/70 absolute right-0.5 bottom-0.5 z-20 rounded p-0.5 opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100"
              >
                <MoreVertical className="size-3.5" aria-hidden="true" />
              </span>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" onClick={(event) => event.stopPropagation()}>
              {appointment.status === "Scheduled" && (
                <DropdownMenuItem onClick={() => onQuickStatusAction(appointment.id, "confirm")}>Confirmar</DropdownMenuItem>
              )}
              {(appointment.status === "Scheduled" || appointment.status === "Confirmed") && (
                <DropdownMenuItem onClick={() => onQuickStatusAction(appointment.id, "start")}>Iniciar</DropdownMenuItem>
              )}
              {appointment.status === "InProgress" && (
                <DropdownMenuItem onClick={() => onQuickStatusAction(appointment.id, "complete")}>Concluir</DropdownMenuItem>
              )}
              {RESCHEDULABLE_STATUSES.includes(appointment.status) && (
                <>
                  <DropdownMenuItem onClick={() => onQuickStatusAction(appointment.id, "noshow")}>Nao compareceu</DropdownMenuItem>
                  <DropdownMenuItem variant="destructive" onClick={() => onQuickStatusAction(appointment.id, "cancel")}>
                    Cancelar
                  </DropdownMenuItem>
                </>
              )}
              <DropdownMenuItem onClick={() => onOpenDetail(appointment.id)}>Ver detalhes</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>

          {isResizable && (
            <div
              role="presentation"
              onPointerDown={handleResizePointerDown}
              onPointerMove={handleResizePointerMove}
              onPointerUp={handleResizePointerUp}
              className="absolute inset-x-0 bottom-0 z-20 h-2 cursor-ns-resize"
            />
          )}
        </div>
      </TooltipTrigger>
      <TooltipContent side="right">
        <div className="flex flex-col gap-0.5">
          <span className="font-medium">{customerName}</span>
          <span>
            {appointment.serviceName} &middot; {formatTimeRange(appointment.startUtc, appointment.endUtc)}
          </span>
          <span>{appointment.price.toLocaleString("pt-BR", { style: "currency", currency: appointment.currency })}</span>
          {isResizable && <span className="opacity-70">Arraste para mover &middot; borda inferior para mudar a duracao</span>}
        </div>
      </TooltipContent>
    </Tooltip>
  );
}
