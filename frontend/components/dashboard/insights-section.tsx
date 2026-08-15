import type { LucideIcon } from "lucide-react";
import { AlertTriangle, TrendingDown, TrendingUp, UserX } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import type { AppointmentStats, CashFlowSummary, CustomerRecoveryCandidate } from "@/lib/api/client";

const REVENUE_DELTA_THRESHOLD = 5;
const NO_SHOW_RATE_THRESHOLD = 15;

type Insight = { icon: LucideIcon; text: string; tone: "positive" | "warning" | "neutral" };

const TONE_CLASSES: Record<Insight["tone"], string> = {
  positive: "text-success",
  warning: "text-destructive",
  neutral: "text-muted-foreground",
};

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
): Insight[] {
  const insights: Insight[] = [];

  if (cashFlow && previousCashFlow) {
    const delta = percentDelta(cashFlow.totalReceived, previousCashFlow.totalReceived);
    if (delta !== null && Math.abs(delta) >= REVENUE_DELTA_THRESHOLD) {
      const rounded = Math.round(Math.abs(delta));
      insights.push(
        delta >= 0
          ? { icon: TrendingUp, text: `Seu faturamento aumentou ${rounded}% em relacao ao periodo anterior.`, tone: "positive" }
          : { icon: TrendingDown, text: `Seu faturamento caiu ${rounded}% em relacao ao periodo anterior.`, tone: "warning" }
      );
    }
  }

  if (stats && stats.totalCount > 0 && stats.noShowRate >= NO_SHOW_RATE_THRESHOLD) {
    insights.push({
      icon: AlertTriangle,
      text: `Sua taxa de no-show foi de ${stats.noShowRate}% neste periodo — considere ativar lembretes automaticos.`,
      tone: "warning",
    });
  }

  if (recoveryCandidates && recoveryCandidates.length > 0) {
    const minDaysSinceLastVisit = Math.min(...recoveryCandidates.map((candidate) => candidate.daysSinceLastVisit));
    const count = recoveryCandidates.length;
    insights.push({
      icon: UserX,
      text: `${count} ${count === 1 ? "cliente esta" : "clientes estao"} ha mais de ${minDaysSinceLastVisit} dias sem retornar.`,
      tone: "neutral",
    });
  }

  return insights;
}

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

  if (insights.length === 0) {
    return null;
  }

  return (
    <div className="mb-8 flex flex-col gap-2">
      {insights.map((insight) => (
        <Card key={insight.text} size="sm">
          <CardContent className="flex items-center gap-3">
            <insight.icon className={`size-5 shrink-0 ${TONE_CLASSES[insight.tone]}`} aria-hidden="true" />
            <p className="text-sm">{insight.text}</p>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
