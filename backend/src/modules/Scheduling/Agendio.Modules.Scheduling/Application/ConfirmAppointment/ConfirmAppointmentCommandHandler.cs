using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.ConfirmAppointment;

public sealed class ConfirmAppointmentCommandHandler(SchedulingDbContext dbContext, IBackgroundJobClient jobClient)
    : ICommandHandler<ConfirmAppointmentCommand>
{
    public async Task<Result> Handle(ConfirmAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
        }

        var result = appointment.Confirm();
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        AppointmentNotificationScheduler.EnqueueConfirmedAttendance(jobClient, appointment.TenantId.Value, appointment.Id.Value);

        return Result.Success();
    }
}
