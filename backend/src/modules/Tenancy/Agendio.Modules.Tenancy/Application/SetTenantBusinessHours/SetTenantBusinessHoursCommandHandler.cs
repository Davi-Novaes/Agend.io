using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.SetTenantBusinessHours;

public sealed class SetTenantBusinessHoursCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<SetTenantBusinessHoursCommand>
{
    public async Task<Result> Handle(SetTenantBusinessHoursCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var entries = request.Entries.Select(e => (e.DayOfWeek, e.StartTime, e.EndTime)).ToList();

        var setResult = tenant.SetBusinessHours(entries);
        if (setResult.IsFailure)
        {
            return setResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
