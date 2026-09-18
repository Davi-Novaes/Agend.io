import type * as React from "react";
import { Minus, Plus } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { CHIP_TONE } from "@/components/agenda/appointment-chip";
import type { AppointmentStatus } from "@/lib/api/client";

export type Density = "compact" | "normal" | "comfortable";

const DENSITY_LABELS: Record<Density, string> = {
  compact: "Compacto",
  normal: "Normal",
  comfortable: "Confortavel",
};

// Rotulo curto proprio da legenda: "Cancelado" basta aqui (o chip e o dialog
// e que precisam distinguir cancelado por cliente vs. equipe), e as duas
// variantes compartilham a mesma cor.
const LEGEND: { key: string; label: string; status?: AppointmentStatus }[] = [
  { key: "Confirmed", label: "Confirmado", status: "Confirmed" },
  { key: "Scheduled", label: "Aguardando", status: "Scheduled" },
  { key: "InProgress", label: "Em atendimento", status: "InProgress" },
  { key: "Completed", label: "Finalizado", status: "Completed" },
  { key: "Cancelled", label: "Cancelado", status: "CancelledByStaff" },
  { key: "NoShow", label: "Faltou", status: "NoShow" },
  { key: "Intervalo", label: "Intervalo" },
];

// 44px por slot de 30min => 1h = 88px, que e a altura em que o card mostra
// horario + status + nome + servico sem cortar nada. Abaixo de 40 o card
// perde a linha de servico (ver appointment-chip), por isso o piso e 32.
export const ZOOM_LEVELS_PX = [32, 44, 56, 72, 88];
export const DEFAULT_ZOOM_PX = 44;

export function AgendaToolbarFooter({
  rowHeightPx,
  onRowHeightChange,
  density,
  onDensityChange,
}: {
  rowHeightPx: number;
  // Recebe o setState direto (nao so um valor) — dois cliques de zoom em
  // sequencia rapida (antes do primeiro re-render assentar) precisam calcular
  // o proximo indice a partir do valor mais recente, nao de um rowHeightPx
  // capturado no closure deste render.
  onRowHeightChange: React.Dispatch<React.SetStateAction<number>>;
  density: Density;
  onDensityChange: (density: Density) => void;
}) {
  const currentIndex = ZOOM_LEVELS_PX.indexOf(rowHeightPx);

  function zoom(direction: -1 | 1) {
    onRowHeightChange((current) => {
      const indexNow = ZOOM_LEVELS_PX.indexOf(current);
      const nextIndex = Math.min(ZOOM_LEVELS_PX.length - 1, Math.max(0, indexNow + direction));
      return ZOOM_LEVELS_PX[nextIndex];
    });
  }

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-3 text-xs">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5">
        {LEGEND.map((item) => (
          <span key={item.key} className="flex items-center gap-1.5">
            <span
              className={`size-2 rounded-full ${item.status ? CHIP_TONE[item.status].dot : "bg-muted-foreground/40"}`}
              aria-hidden="true"
            />
            <span className="text-muted-foreground">{item.label}</span>
          </span>
        ))}
      </div>

      <div className="flex items-center gap-2">
        <div className="flex items-center overflow-hidden rounded-lg border">
          <Button variant="ghost" size="sm" className="rounded-none" onClick={() => zoom(-1)} disabled={currentIndex <= 0} aria-label="Diminuir zoom">
            <Minus className="size-3.5" />
          </Button>
          <span className="text-muted-foreground w-14 border-x px-2 text-center tabular-nums">
            {Math.round((rowHeightPx / DEFAULT_ZOOM_PX) * 100)}%
          </span>
          <Button
            variant="ghost"
            size="sm"
            className="rounded-none"
            onClick={() => zoom(1)}
            disabled={currentIndex >= ZOOM_LEVELS_PX.length - 1}
            aria-label="Aumentar zoom"
          >
            <Plus className="size-3.5" />
          </Button>
        </div>

        <Select value={density} onValueChange={(value) => onDensityChange(value as Density)}>
          <SelectTrigger size="sm" className="w-32" aria-label="Densidade">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(Object.keys(DENSITY_LABELS) as Density[]).map((value) => (
              <SelectItem key={value} value={value}>
                {DENSITY_LABELS[value]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
