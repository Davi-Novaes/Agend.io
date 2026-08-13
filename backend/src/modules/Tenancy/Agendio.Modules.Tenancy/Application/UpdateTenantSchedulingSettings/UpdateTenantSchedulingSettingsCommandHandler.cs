using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantSchedulingSettings;

public sealed class UpdateTenantSchedulingSettingsCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<UpdateTenantSchedulingSettingsCommand>
{
    public async Task<Result> Handle(UpdateTenantSchedulingSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var closedDates = request.ClosedDates.Select(d => (d.Date, d.Reason)).ToList();
        var updateResult = tenant.UpdateSchedulingSettings(closedDates, request.AppointmentBufferMinutes);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
