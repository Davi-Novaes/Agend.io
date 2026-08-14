using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.CancelAppointment;

public sealed class CancelAppointmentCommandHandler(SchedulingDbContext dbContext, IBackgroundJobClient jobClient)
    : ICommandHandler<CancelAppointmentCommand>
{
    public async Task<Result> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
        }

        var result = request.ByStaff ? appointment.CancelByStaff() : appointment.CancelByCustomer();
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        AppointmentNotificationScheduler.EnqueueCancellation(jobClient, appointment.TenantId.Value, appointment.Id.Value);

        return Result.Success();
    }
}
