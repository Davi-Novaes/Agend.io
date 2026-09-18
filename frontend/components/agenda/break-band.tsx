export type BlockedBandKind = "Intervalo" | "ForaDoExpediente" | "Ausente";

export type BlockedBand = {
  top: number;
  height: number;
  kind: BlockedBandKind;
  /** "13:00 - 14:00" — so preenchido no Intervalo, onde o horario exato importa. */
  timeRange?: string;
};

const LABEL: Record<BlockedBandKind, string> = {
  Intervalo: "Intervalo",
  ForaDoExpediente: "Fora do expediente",
  Ausente: "Ausente",
};

/**
 * Decorativo, atras dos botoes de slot e dos chips (z-0) — so sinaliza
 * visualmente; quem impede o clique-pra-criar e o `disabled` no botao de slot
 * correspondente (ver renderTimeGrid).
 *
 * Intervalo ganha caixa propria (e um horario), porque e uma pausa real do
 * profissional que a recepcao precisa enxergar. Fora do expediente/ausente
 * ficam so com uma hachura discreta: sao "nao-espaco", nao devem competir
 * visualmente com os agendamentos.
 */
export function BreakBand({ band }: { band: BlockedBand }) {
  if (band.kind === "Intervalo") {
    return (
      <div
        className="border-border/70 bg-muted/30 text-muted-foreground pointer-events-none absolute inset-x-1 z-0 flex flex-col justify-center overflow-hidden rounded-md border border-dashed px-2"
        style={{ top: band.top, height: band.height }}
      >
        {band.height >= 34 && band.timeRange && <span className="truncate text-[10px] opacity-80">{band.timeRange}</span>}
        <span className="truncate text-[11px] font-medium">{LABEL[band.kind]}</span>
      </div>
    );
  }

  return (
    <div
      className="text-muted-foreground/70 pointer-events-none absolute inset-x-0 z-0 flex items-center justify-center overflow-hidden"
      style={{
        top: band.top,
        height: band.height,
        backgroundImage:
          "repeating-linear-gradient(135deg, transparent, transparent 7px, color-mix(in oklch, var(--border) 60%, transparent) 7px, color-mix(in oklch, var(--border) 60%, transparent) 8px)",
      }}
    >
      {band.height >= 28 && <span className="text-[10px] tracking-wide">{LABEL[band.kind]}</span>}
    </div>
  );
}
