using System.Globalization;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.GetReviewsSummary;

public sealed class GetReviewsSummaryQueryHandler(SchedulingDbContext dbContext, IResourceLookupService resourceLookupService)
    : IQueryHandler<GetReviewsSummaryQuery, ReviewsSummary>
{
    private const int RecentReviewsLimit = 10;

    public async Task<Result<ReviewsSummary>> Handle(GetReviewsSummaryQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtcExclusive = new DateTimeOffset(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var reviews = await dbContext.Reviews.AsNoTracking()
            .Where(r => r.CreatedAtUtc >= fromUtc && r.CreatedAtUtc < toUtcExclusive)
            .Select(r => new
            {
                r.Id, r.ServiceName, r.ResourceId, r.Rating, r.Comment, r.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var totalCount = reviews.Count;
        var averageRating = totalCount == 0 ? 0m : Math.Round((decimal)reviews.Average(r => r.Rating), 2);

        var seriesByMonth = reviews
            .Select(r => (r.CreatedAtUtc.Year, r.CreatedAtUtc.Month, r.Rating))
            .GroupBy(point => (point.Year, point.Month))
            .OrderBy(group => group.Key.Year).ThenBy(group => group.Key.Month)
            .Select(group => new ReviewMonthPoint(
                FormatMonth(group.Key.Year, group.Key.Month), Math.Round((decimal)group.Average(point => point.Rating), 2), group.Count()))
            .ToList();

        var byService = reviews
            .GroupBy(r => r.ServiceName)
            .Select(group => new ServiceRatingPoint(group.Key, Math.Round((decimal)group.Average(r => r.Rating), 2), group.Count()))
            .OrderByDescending(point => point.Count)
            .ToList();

        var byResourceId = reviews
            .GroupBy(r => r.ResourceId)
            .Select(group => new { ResourceId = group.Key, Average = Math.Round((decimal)group.Average(r => r.Rating), 2), Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ToList();

        var byProfessional = new List<ProfessionalRatingPoint>();
        foreach (var group in byResourceId)
        {
            var resource = await resourceLookupService.FindByIdAsync(group.ResourceId, cancellationToken);
            byProfessional.Add(new ProfessionalRatingPoint(group.ResourceId, resource?.Name ?? "Profissional removido", group.Average, group.Count));
        }

        var recentReviews = reviews
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(RecentReviewsLimit)
            .Select(r => new RecentReviewPoint(r.Id.Value, r.ServiceName, r.Rating, r.Comment, r.CreatedAtUtc))
            .ToList();

        return Result.Success(new ReviewsSummary(averageRating, totalCount, seriesByMonth, byService, byProfessional, recentReviews));
    }

    private static string FormatMonth(int year, int month) =>
        string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{month:D2}");
}
