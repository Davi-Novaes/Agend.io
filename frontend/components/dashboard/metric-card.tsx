import type { LucideIcon } from "lucide-react";
import { TrendingDown, TrendingUp } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";

function formatDelta(delta: number): string {
  const rounded = Math.abs(delta).toLocaleString("pt-BR", { maximumFractionDigits: 1, minimumFractionDigits: 1 });
  return `${delta >= 0 ? "+" : "-"}${rounded}%`;
}

export function MetricCard({
  icon: Icon,
  title,
  value,
  delta,
  deltaLabel = "vs. periodo anterior",
  description,
  isLoading = false,
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
}) {
  const hasDelta = !isLoading && delta !== null && delta !== undefined && Number.isFinite(delta);
  const isPositive = hasDelta && delta >= 0;

  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground text-sm">{title}</span>
          <Icon className="text-muted-foreground size-4" aria-hidden="true" />
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
