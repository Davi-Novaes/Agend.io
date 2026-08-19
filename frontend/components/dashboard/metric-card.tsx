import type { LucideIcon } from "lucide-react";
import { TrendingDown, TrendingUp } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

function formatDelta(delta: number): string {
  const rounded = Math.abs(delta).toLocaleString("pt-BR", { maximumFractionDigits: 1, minimumFractionDigits: 1 });
  return `${delta >= 0 ? "+" : "-"}${rounded}%`;
}

type Tone = "primary" | "destructive" | "success" | "info";

const TONE_CLASSES: Record<Tone, string> = {
  primary: "bg-primary/15 text-primary",
  destructive: "bg-destructive/15 text-destructive",
  success: "bg-success/15 text-success",
  info: "bg-info/15 text-info",
};

export function MetricCard({
  icon: Icon,
  title,
  value,
  delta,
  deltaLabel = "vs. periodo anterior",
  description,
  isLoading = false,
  tone = "primary",
}: {
  icon: LucideIcon;
  title: string;
  value: string;
  /** Omitir quando o periodo anterior nao tiver base de comparacao (ex.: tenant novo, sem historico). */
  delta?: number | null;
  deltaLabel?: string;
  /** Legenda curta abaixo do valor — usado por KPIs cujo calculo nao e obvio (ex.: Resultado). */
  description?: string;
  /** Enquanto true, ignora `value`/`delta` e mostra skeleton no lugar (query ainda em voo). */
  isLoading?: boolean;
  /** Cor do badge do icone — puramente decorativo, nao carrega significado alem de diferenciar os cards. */
  tone?: Tone;
}) {
  const hasDelta = !isLoading && delta !== null && delta !== undefined && Number.isFinite(delta);
  const isPositive = hasDelta && delta >= 0;

  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground text-sm">{title}</span>
          <div className={cn("flex size-9 items-center justify-center rounded-lg", TONE_CLASSES[tone])}>
            <Icon className="size-4.5" aria-hidden="true" />
          </div>
        </div>
        {isLoading ? <Skeleton className="h-8 w-24" /> : <span className="text-2xl font-semibold tabular-nums">{value}</span>}
        {description && !isLoading && <p className="text-muted-foreground -mt-2 text-xs">{description}</p>}
        {hasDelta && (
          <div className="flex items-center gap-1.5">
            <Badge variant={isPositive ? "success" : "destructive"}>
              {isPositive ? (
                <TrendingUp data-icon="inline-start" aria-hidden="true" />
              ) : (
                <TrendingDown data-icon="inline-start" aria-hidden="true" />
              )}
              {formatDelta(delta)}
            </Badge>
            <span className="text-muted-foreground text-xs">{deltaLabel}</span>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
