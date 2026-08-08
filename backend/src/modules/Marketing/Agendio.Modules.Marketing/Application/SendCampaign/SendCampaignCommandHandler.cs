using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Marketing.Infrastructure.Notifications;
using Agendio.Modules.Marketing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Hangfire;

namespace Agendio.Modules.Marketing.Application.SendCampaign;

public sealed class SendCampaignCommandHandler(
    MarketingDbContext dbContext, ITenantContext tenantContext, IClock clock, ICustomerLookupService customerLookup,
    IBackgroundJobClient jobClient) : ICommandHandler<SendCampaignCommand, SendCampaignResult>
{
    public async Task<Result<SendCampaignResult>> Handle(SendCampaignCommand request, CancellationToken cancellationToken)
    {
        var recipients = await customerLookup.ListActiveWithEmailAsync(cancellationToken);

        var campaignResult = Domain.Campaign.Create(tenantContext.TenantId, request.Subject, request.Body, recipients.Count, clock.UtcNow);
        if (campaignResult.IsFailure)
        {
            return Result.Failure<SendCampaignResult>(campaignResult.Error);
        }

        dbContext.Campaigns.Add(campaignResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Enfileira DEPOIS do commit: se o Hangfire falhar em pegar o job, a
        // Campaign ja esta correta no banco — o contrario arriscaria disparar
        // e-mail para uma campanha que acabou nao sendo persistida.
        foreach (var recipient in recipients)
        {
            jobClient.Enqueue<CampaignEmailJob>(job =>
                job.SendAsync(tenantContext.TenantId.Value, recipient.Email!, recipient.FullName, request.Subject, request.Body, CancellationToken.None));
        }

        return Result.Success(new SendCampaignResult(campaignResult.Value.Id.Value, recipients.Count));
    }
}
