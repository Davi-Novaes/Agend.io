using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.GetReviewsSummary;

public sealed record GetReviewsSummaryQuery(DateOnly From, DateOnly To) : IQuery<ReviewsSummary>;

public sealed record ReviewMonthPoint(string Month, decimal AverageRating, int Count);

public sealed record ServiceRatingPoint(string ServiceName, decimal AverageRating, int Count);

public sealed record ProfessionalRatingPoint(Guid ResourceId, string ResourceName, decimal AverageRating, int Count);

public sealed record RecentReviewPoint(Guid Id, string ServiceName, int Rating, string? Comment, DateTimeOffset CreatedAtUtc);

public sealed record ReviewsSummary(
    decimal AverageRating,
    int TotalCount,
    IReadOnlyList<ReviewMonthPoint> SeriesByMonth,
    IReadOnlyList<ServiceRatingPoint> ByService,
    IReadOnlyList<ProfessionalRatingPoint> ByProfessional,
    IReadOnlyList<RecentReviewPoint> RecentReviews);
