import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { AlertTriangle, CircleCheck, Receipt, Sparkles, Star, TrendingDown, TrendingUp, UserX } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import type { AppointmentStats, CashFlowSummary, CustomerRecoveryCandidate } from "@/lib/api/client";
import { cn } from "@/lib/utils";

const CANCELLATION_RATE_WARNING_THRESHOLD = 10;

type Tone = "positive" | "warning" | "neutral";

const ICON_TONE_CLASSES: Record<Tone, string> = {
  positive: "bg-success/15 text-success",
  warning: "bg-destructive/15 text-destructive",
  neutral: "bg-muted text-muted-foreground",
};

type InsightRow = {
  key: string;
  icon: LucideIcon;
  label: string;
  value: string;
  meta?: string;
  tone: Tone;
};

function formatCurrency(value: number): string {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

/** null quando o periodo anterior nao tem base pra comparar (ex.: tenant novo, sem historico). */
function percentDelta(current: number, previous: number): number | null {
  if (previous === 0) return null;
  return ((current - previous) / previous) * 100;
}

function buildInsights(
  cashFlow: CashFlowSummary | undefined,
  previousCashFlow: CashFlowSummary | undefined,
  stats: AppointmentStats | undefined,
  recoveryCandidates: CustomerRecoveryCandidate[] | undefined
): InsightRow[] {
  const rows: InsightRow[] = [];

  if (cashFlow && previousCashFlow) {
    const delta = percentDelta(cashFlow.totalReceived, previousCashFlow.totalReceived);
    if (delta !== null) {
      const isPositive = delta >= 0;
      rows.push({
        key: "revenue",
        icon: isPositive ? TrendingUp : TrendingDown,
        label: "Faturamento",
        value: `${isPositive ? "+" : ""}${Math.round(delta)}%`,
        meta: "vs. periodo anterior",
        tone: isPositive ? "positive" : "warning",
      });
    }
  }

  if (stats && stats.revenueByService.length > 0) {
    const topService = [...stats.revenueByService].sort((a, b) => b.total - a.total)[0];
    rows.push({
      key: "top-service",
      icon: Star,
      label: "Servico mais rentavel",
      value: topService.serviceName,
      meta: formatCurrency(topService.total),
      tone: "neutral",
    });
  }

  if (cashFlow && stats && stats.completedCount > 0) {
    rows.push({
      key: "avg-ticket",
      icon: Receipt,
      label: "Ticket medio",
      value: formatCurrency(cashFlow.totalReceived / stats.completedCount),
      meta: `${stats.completedCount} atendimentos concluidos`,
      tone: "neutral",
    });
  }

  if (stats && stats.totalCount > 0) {
    const isHigh = stats.cancellationRate >= CANCELLATION_RATE_WARNING_THRESHOLD;
    rows.push({
      key: "cancellation",
      icon: isHigh ? AlertTriangle : CircleCheck,
      label: "Cancelamentos",
      value: `${stats.cancellationRate}%`,
      meta: isHigh ? "acima do esperado" : "sob controle",
      tone: isHigh ? "warning" : "positive",
    });
  }

  if (recoveryCandidates && recoveryCandidates.length > 0) {
    const minDaysSinceLastVisit = Math.min(...recoveryCandidates.map((candidate) => candidate.daysSinceLastVisit));
    const count = recoveryCandidates.length;
    rows.push({
      key: "recovery",
      icon: UserX,
      label: count === 1 ? "Cliente para recuperar" : "Clientes para recuperar",
      value: String(count),
      meta: `ha pelo menos ${minDaysSinceLastVisit} dias sem retornar`,
      tone: "neutral",
    });
  }

  return rows.slice(0, 5);
}

// "Sparkles" e so um icone de destaque, nao uma alegacao de IA — o conteudo
// aqui e 100% regra fixa (calculos abaixo), sem LLM envolvido. Ver "Ver
// analise completa" como saida honesta pra quem quer mais detalhe (pagina real).
export function InsightsSection({
  cashFlow,
  previousCashFlow,
  stats,
  recoveryCandidates,
}: {
  cashFlow: CashFlowSummary | undefined;
  previousCashFlow: CashFlowSummary | undefined;
  stats: AppointmentStats | undefined;
  recoveryCandidates: CustomerRecoveryCandidate[] | undefined;
}) {
  const insights = buildInsights(cashFlow, previousCashFlow, stats, recoveryCandidates);

  return (
    <Card className="border-border/70 ring-0 shadow-none">
      <CardContent>
        <div className="mb-3 flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Sparkles className="text-primary size-4" aria-hidden="true" />
            <h3 className="text-sm font-semibold">Insights do periodo</h3>
          </div>
          <Link href="/relatorios" className="text-primary text-xs font-medium hover:underline">
            Ver analise completa →
          </Link>
        </div>

        {insights.length === 0 ? (
          <p className="text-muted-foreground py-6 text-center text-sm">
            Ainda estamos reunindo dados para gerar insights para o seu negocio.
          </p>
        ) : (
          <ul className="flex flex-col divide-y">
            {insights.map((insight) => (
              <li key={insight.key} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0">
                <div className={cn("flex size-8 shrink-0 items-center justify-center rounded-lg", ICON_TONE_CLASSES[insight.tone])}>
                  <insight.icon className="size-4" aria-hidden="true" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-muted-foreground truncate text-xs">{insight.label}</p>
                  {insight.meta && <p className="text-muted-foreground truncate text-[11px]">{insight.meta}</p>}
                </div>
                <span
                  className={cn(
                    "shrink-0 truncate text-sm font-semibold tabular-nums",
                    insight.tone === "positive" && "text-success",
                    insight.tone === "warning" && "text-destructive"
                  )}
                >
                  {insight.value}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
