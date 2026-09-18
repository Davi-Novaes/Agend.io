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
  /** Mensagem quando o KPI nao tem nenhum dado ainda (ex.: R$ 0,00 num tenant novo) —
      substitui o delta/descricao por algo orientado a acao em vez de deixar o card
      parecer quebrado ("Aguardando primeiros lancamentos" em vez de espaco vazio). */
  emptyLabel,
  isLoading = false,
  tone = "primary",
  featured = false,
}: {
  icon: LucideIcon;
  title: string;
  value: string;
  /** Omitir quando o periodo anterior nao tiver base de comparacao (ex.: tenant novo, sem historico). */
  delta?: number | null;
  deltaLabel?: string;
  /** Legenda curta abaixo do valor — usado por KPIs cujo calculo nao e obvio (ex.: Resultado). */
  description?: string;
  emptyLabel?: string;
  /** Enquanto true, ignora `value`/`delta` e mostra skeleton no lugar (query ainda em voo). */
  isLoading?: boolean;
  /** Cor do badge do icone — puramente decorativo, nao carrega significado alem de diferenciar os cards. */
  tone?: Tone;
  /** Destaque visual pro KPI que o dono do negocio mais olha (Faturamento) -- nao muda o layout do grid, so o peso visual do proprio card. */
  featured?: boolean;
}) {
  const hasDelta = !isLoading && delta !== null && delta !== undefined && Number.isFinite(delta);
  const isPositive = hasDelta && delta >= 0;
  // emptyLabel so vem preenchido quando quem chama ja confirmou que o valor
  // bruto e zero (ver painel/page.tsx) -- aqui so decide entre ele e o delta,
  // nunca infere "vazio" sozinho (senao mostraria a mensagem errada sempre
  // que so faltasse base de comparacao, mesmo com receita real no periodo).
  const showEmptyLabel = !isLoading && !hasDelta && !description && Boolean(emptyLabel);

  return (
    <Card
      size="sm"
      className={cn(
        "ring-0 shadow-sm transition-shadow duration-200 hover:shadow-md",
        featured && "bg-primary/[0.04] shadow-primary/5"
      )}
    >
      <CardContent className="flex flex-col gap-2">
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{title}</span>
          <div className={cn("flex size-7 items-center justify-center rounded-md", TONE_CLASSES[tone])}>
            <Icon className="size-3.5" aria-hidden="true" />
          </div>
        </div>
        {isLoading ? (
          <Skeleton className="h-8 w-24" />
        ) : (
          <span className={cn("font-semibold tabular-nums", featured ? "text-2xl" : "text-xl")}>{value}</span>
        )}
        {description && !isLoading && <p className="text-muted-foreground text-xs">{description}</p>}
        {showEmptyLabel && <p className="text-muted-foreground text-xs">{emptyLabel}</p>}
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
