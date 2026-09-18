using Agendio.Modules.Feedback.Contracts;
using Agendio.Modules.Feedback.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Feedback.Infrastructure;

public sealed class FeedbackReader(FeedbackDbContext dbContext) : IFeedbackReader
{
    public async Task<IReadOnlyList<FeedbackEntrySummary>> GetRecentAsync(int maxEntries, CancellationToken cancellationToken = default) =>
        await dbContext.FeedbackEntries
            .AsNoTracking()
            .OrderByDescending(f => f.CreatedAtUtc)
            .Take(maxEntries)
            .Select(f => new FeedbackEntrySummary(
                f.Id.Value, f.TenantId.Value, f.SubmittedByUserId, f.Subject, f.Body, f.CreatedAtUtc))
            .ToListAsync(cancellationToken);
}
