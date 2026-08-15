using Agendio.Modules.Scheduling.Application.GetReviewsSummary;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Infrastructure;

// Delega para o handler ja existente via IDispatcher (chamada intra-modulo,
// permitida) em vez de duplicar a logica de agregacao aqui. RecentReviews
// (comentarios livres do cliente) fica de fora do Lookup de proposito — o
// Assistente (Fase 22) so recebe agregados, nunca texto livre de terceiros.
public sealed class ReviewsSummaryLookupService(IDispatcher dispatcher) : IReviewsSummaryLookupService
{
    public async Task<ReviewsSummaryLookupResult> GetSummaryAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.Query(new GetReviewsSummaryQuery(from, to), cancellationToken);
        var summary = result.Value;

        return new ReviewsSummaryLookupResult(
            summary.AverageRating,
            summary.TotalCount,
            summary.SeriesByMonth.Select(p => new ReviewMonthLookupPoint(p.Month, p.AverageRating, p.Count)).ToList(),
            summary.ByService.Select(p => new ServiceRatingLookupPoint(p.ServiceName, p.AverageRating, p.Count)).ToList(),
            summary.ByProfessional.Select(p => new ProfessionalRatingLookupPoint(p.ResourceId, p.ResourceName, p.AverageRating, p.Count)).ToList());
    }
}
