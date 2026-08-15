namespace Agendio.Modules.Scheduling.Contracts;

/// <summary>
/// Leitura agregada de avaliacoes — usada pelo Assistente (Fase 22) para
/// responder perguntas em linguagem natural sem o modulo Assistant precisar ler
/// tabela do Scheduling diretamente.
/// </summary>
public interface IReviewsSummaryLookupService
{
    Task<ReviewsSummaryLookupResult> GetSummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed record ReviewMonthLookupPoint(string Month, decimal AverageRating, int Count);

public sealed record ServiceRatingLookupPoint(string ServiceName, decimal AverageRating, int Count);

public sealed record ProfessionalRatingLookupPoint(Guid ResourceId, string ResourceName, decimal AverageRating, int Count);

public sealed record ReviewsSummaryLookupResult(
    decimal AverageRating,
    int TotalCount,
    IReadOnlyList<ReviewMonthLookupPoint> SeriesByMonth,
    IReadOnlyList<ServiceRatingLookupPoint> ByService,
    IReadOnlyList<ProfessionalRatingLookupPoint> ByProfessional);
