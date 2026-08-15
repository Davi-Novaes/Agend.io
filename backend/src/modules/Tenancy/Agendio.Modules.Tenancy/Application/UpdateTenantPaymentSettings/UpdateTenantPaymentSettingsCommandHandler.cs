using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPaymentSettings;

public sealed class UpdateTenantPaymentSettingsCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<UpdateTenantPaymentSettingsCommand>
{
    public async Task<Result> Handle(UpdateTenantPaymentSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var requirement = request.PaymentRequired ? PaymentRequirement.Deposit : PaymentRequirement.None;
        var updateResult = tenant.UpdatePaymentSettings(requirement, request.DepositPercentage);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
