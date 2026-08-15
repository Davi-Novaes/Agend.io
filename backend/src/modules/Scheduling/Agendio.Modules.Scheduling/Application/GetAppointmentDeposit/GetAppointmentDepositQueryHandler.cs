using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.GetAppointmentDeposit;

public sealed class GetAppointmentDepositQueryHandler(SchedulingDbContext dbContext)
    : IQueryHandler<GetAppointmentDepositQuery, AppointmentDepositSummary?>
{
    public async Task<Result<AppointmentDepositSummary?>> Handle(GetAppointmentDepositQuery request, CancellationToken cancellationToken)
    {
        var deposit = await dbContext.AppointmentDeposits.AsNoTracking()
            .SingleOrDefaultAsync(d => d.AppointmentId == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (deposit is null)
        {
            return Result.Success<AppointmentDepositSummary?>(null);
        }

        var summary = new AppointmentDepositSummary(
            deposit.Amount.Amount, deposit.Amount.Currency, deposit.Status.ToString(), deposit.InvoiceUrl, deposit.PaidAtUtc);
        return Result.Success<AppointmentDepositSummary?>(summary);
    }
}
