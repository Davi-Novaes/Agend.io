using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Scheduling.Contracts;

namespace Agendio.Modules.Customers.Application.ListCustomers;

/// <summary>
/// Fase 9 — auto-segmentacao. Prioridade fixa (primeiro match vence), cada
/// cliente cai em exatamente um segmento — pensado pra virar filtro de lista,
/// nao um conjunto de badges independentes. "Em risco"/"Inativo" usam uma
/// janela fixa (45/90 dias) — deliberadamente mais simples que a deteccao por
/// intervalo pessoal de retorno da Fase 10 (recuperacao de clientes), que ainda
/// nao foi implementada.
/// </summary>
public static class CustomerSegmentCalculator
{
    private const int AtRiskAfterDays = 45;
    private const int InactiveAfterDays = 90;
    private const int NoShowThreshold = 2;
    private const int VipMinimumVisits = 5;
    private const int VipMinimumSampleSize = 5;
    private const double VipSpendPercentile = 0.8;

    public static CustomerSegment Calculate(CustomerVisitStatsLookupResult? stats, DateTimeOffset nowUtc, decimal vipSpendThreshold)
    {
        if (stats is null || stats.TotalVisits == 0)
        {
            return CustomerSegment.Novo;
        }

        if (stats.NoShowCount >= NoShowThreshold)
        {
            return CustomerSegment.NoShow;
        }

        var daysSinceLastVisit = stats.LastVisitAtUtc is { } lastVisitAtUtc
            ? (nowUtc - lastVisitAtUtc).TotalDays
            : double.MaxValue;

        if (daysSinceLastVisit > InactiveAfterDays)
        {
            return CustomerSegment.Inativo;
        }

        if (daysSinceLastVisit > AtRiskAfterDays)
        {
            return CustomerSegment.EmRisco;
        }

        if (stats.TotalVisits >= VipMinimumVisits && stats.TotalSpent >= vipSpendThreshold)
        {
            return CustomerSegment.Vip;
        }

        return CustomerSegment.Recorrente;
    }

    /// <summary>
    /// Percentil 80 de gasto entre clientes com pelo menos uma visita — "VIP" e
    /// relativo ao proprio tenant, sem limiar fixo em reais (que seria arbitrario
    /// e dependeria de moeda/porte do negocio). Amostra menor que
    /// <see cref="VipMinimumSampleSize"/> nao produz VIP nenhum: "top 20%" nao
    /// faz sentido com poucos clientes.
    /// </summary>
    public static decimal CalculateVipSpendThreshold(IReadOnlyCollection<CustomerVisitStatsLookupResult> statsWithVisits)
    {
        if (statsWithVisits.Count < VipMinimumSampleSize)
        {
            return decimal.MaxValue;
        }

        var sortedSpend = statsWithVisits.Select(s => s.TotalSpent).OrderBy(amount => amount).ToList();
        // topCount = quantos clientes cabem no "top 20%" (arredondado pra cima,
        // minimo 1); o limiar e o gasto do menor deles.
        var topCount = Math.Max(1, (int)Math.Ceiling(sortedSpend.Count * (1 - VipSpendPercentile)));
        var index = Math.Clamp(sortedSpend.Count - topCount, 0, sortedSpend.Count - 1);
        return sortedSpend[index];
    }
}
