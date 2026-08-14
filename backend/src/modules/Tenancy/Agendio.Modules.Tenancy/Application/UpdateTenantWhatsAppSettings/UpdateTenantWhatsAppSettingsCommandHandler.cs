using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantWhatsAppSettings;

public sealed class UpdateTenantWhatsAppSettingsCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<UpdateTenantWhatsAppSettingsCommand>
{
    public async Task<Result> Handle(UpdateTenantWhatsAppSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        // O token nunca volta em texto puro pela API (ver GetTenantProfileQuery)
        // — um valor vazio aqui significa "o dono nao digitou um novo", entao
        // mantem o que ja estava salvo em vez de apagar a integracao.
        var accessTokenToUse = string.IsNullOrWhiteSpace(request.AccessToken) ? tenant.WhatsAppAccessToken : request.AccessToken;

        var updateResult = tenant.UpdateWhatsAppSettings(
            request.Enabled,
            request.PhoneNumberId,
            accessTokenToUse,
            request.ScheduledTemplate,
            request.ReminderTemplate,
            request.CancelledTemplate,
            request.RescheduledTemplate,
            request.ConfirmedTemplate,
            request.CompletedTemplate);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
