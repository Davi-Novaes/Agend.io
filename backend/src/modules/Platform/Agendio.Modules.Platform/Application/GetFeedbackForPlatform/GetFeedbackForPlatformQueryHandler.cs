using Agendio.Modules.Feedback.Contracts;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Platform.Application.GetFeedbackForPlatform;

public sealed class GetFeedbackForPlatformQueryHandler(
    IFeedbackReader feedbackReader,
    ITenantAdministrationService tenantAdministrationService) : IQueryHandler<GetFeedbackForPlatformQuery, IReadOnlyList<PlatformFeedbackEntry>>
{
    private const int MaxEntries = 300;

    public async Task<Result<IReadOnlyList<PlatformFeedbackEntry>>> Handle(GetFeedbackForPlatformQuery request, CancellationToken cancellationToken)
    {
        var entries = await feedbackReader.GetRecentAsync(MaxEntries, cancellationToken);

        var tenants = await tenantAdministrationService.ListAllAsync(cancellationToken);
        var tenantNameById = tenants.ToDictionary(t => t.TenantId, t => t.Name);

        var result = entries
            .Select(e => new PlatformFeedbackEntry(
                e.Id,
                tenantNameById.TryGetValue(TenantId.From(e.TenantId), out var name) ? name : null,
                e.Subject,
                e.Body,
                e.CreatedAtUtc))
            .ToList();

        return Result.Success<IReadOnlyList<PlatformFeedbackEntry>>(result);
    }
}
