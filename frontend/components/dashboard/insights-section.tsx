import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { AlertTriangle, Sparkles, TrendingDown, TrendingUp, UserX } from "lucide-react";
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

// "Sparkles" e so um icone de destaque, nao uma alegacao de IA — o conteudo
// aqui e 100% regra fixa (thresholds acima), sem LLM envolvido. Ver "Ver
// relatorios" como saida honesta pra quem quer mais detalhe (pagina real).
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
    <div className="rounded-lg border p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Sparkles className="text-primary size-4" aria-hidden="true" />
          <h3 className="text-sm font-semibold">Resumo do periodo</h3>
        </div>
        <Link href="/relatorios" className="text-primary text-xs font-medium hover:underline">
          Ver relatorios →
        </Link>
      </div>

      {insights.length === 0 ? (
        <p className="text-muted-foreground py-6 text-center text-sm">
          Sem destaques por enquanto — volte quando houver mais movimentacao no periodo.
        </p>
      ) : (
        <ul className="flex flex-col gap-2.5">
          {insights.map((insight) => (
            <li key={insight.text} className="flex items-start gap-2.5">
              <insight.icon className={`mt-0.5 size-4 shrink-0 ${TONE_CLASSES[insight.tone]}`} aria-hidden="true" />
              <p className="text-sm">{insight.text}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
