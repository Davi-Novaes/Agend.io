using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Marketing.Domain;
using Agendio.Modules.Marketing.Infrastructure.Notifications;
using Agendio.Modules.Marketing.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Hangfire;

namespace Agendio.Modules.Marketing.Application.SendCampaign;

public sealed class SendCampaignCommandHandler(
    MarketingDbContext dbContext, ITenantContext tenantContext, IClock clock, ICustomerLookupService customerLookup,
    ITenantLookupService tenantLookup, IBackgroundJobClient jobClient) : ICommandHandler<SendCampaignCommand, SendCampaignResult>
{
    public async Task<Result<SendCampaignResult>> Handle(SendCampaignCommand request, CancellationToken cancellationToken)
    {
        // Valida ANTES de tocar em qualquer estado — WhatsApp desconfigurado nao
        // pode deixar uma Campaign "enviada" com zero destinatarios de verdade.
        if (request.Channel == CampaignChannel.WhatsApp)
        {
            var settings = await tenantLookup.GetWhatsAppSettingsAsync(tenantContext.TenantId, cancellationToken);
            if (settings is null || !settings.Enabled || settings.PhoneNumberId is null || settings.AccessToken is null)
            {
                return Result.Failure<SendCampaignResult>(Error.Validation(
                    "Campaign.WhatsAppNotConfigured",
                    "WhatsApp nao esta configurado para este estabelecimento. Configure em Configuracoes > WhatsApp."));
            }
        }

        var candidates = await customerLookup.ListActiveBySegmentAsync(request.TargetSegment, cancellationToken);
        var recipients = request.Channel == CampaignChannel.Email
            ? candidates.Where(c => c.Email is not null).ToList()
            : candidates.Where(c => c.Phone is not null).ToList();

        var campaignResult = Domain.Campaign.Create(
            tenantContext.TenantId, request.Subject, request.Body, request.Channel, request.TargetSegment?.ToString(),
            recipients.Count, clock.UtcNow);
        if (campaignResult.IsFailure)
        {
            return Result.Failure<SendCampaignResult>(campaignResult.Error);
        }

        dbContext.Campaigns.Add(campaignResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Enfileira DEPOIS do commit: se o Hangfire falhar em pegar o job, a
        // Campaign ja esta correta no banco — o contrario arriscaria disparar
        // mensagem para uma campanha que acabou nao sendo persistida.
        var tenantId = tenantContext.TenantId.Value;
        foreach (var recipient in recipients)
        {
            if (request.Channel == CampaignChannel.Email)
            {
                jobClient.Enqueue<CampaignEmailJob>(job =>
                    job.SendAsync(tenantId, recipient.Email!, recipient.FullName, request.Subject, request.Body, CancellationToken.None));
            }
            else
            {
                jobClient.Enqueue<CampaignWhatsAppJob>(job =>
                    job.SendAsync(tenantId, recipient.Phone!, recipient.FullName, request.Body, CancellationToken.None));
            }
        }

        return Result.Success(new SendCampaignResult(campaignResult.Value.Id.Value, recipients.Count));
    }
}
