using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.GetAppointmentById;

public sealed class GetAppointmentByIdQueryHandler(SchedulingDbContext dbContext) : IQueryHandler<GetAppointmentByIdQuery, AppointmentDetails>
{
    public async Task<Result<AppointmentDetails>> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (appointment is null)
        {
            return Result.Failure<AppointmentDetails>(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
        }

        var details = new AppointmentDetails(
            appointment.Id.Value, appointment.CustomerId, appointment.ResourceId, appointment.ServiceId, appointment.ServiceName,
            appointment.Slot.StartUtc, appointment.Slot.EndUtc, appointment.Status.ToString(),
            appointment.Price.Amount, appointment.Price.Currency, appointment.Notes);

        return Result.Success(details);
    }
}
