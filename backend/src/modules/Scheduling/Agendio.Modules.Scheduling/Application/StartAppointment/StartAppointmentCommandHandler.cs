using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.StartAppointment;

public sealed class StartAppointmentCommandHandler(SchedulingDbContext dbContext) : ICommandHandler<StartAppointmentCommand>
{
    public async Task<Result> Handle(StartAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
        }

        var result = appointment.Start();
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
