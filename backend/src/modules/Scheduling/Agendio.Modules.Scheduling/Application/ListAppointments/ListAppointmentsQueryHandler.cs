using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.ListAppointments;

public sealed class ListAppointmentsQueryHandler(SchedulingDbContext dbContext)
    : IQueryHandler<ListAppointmentsQuery, IReadOnlyList<AppointmentSummary>>
{
    public async Task<Result<IReadOnlyList<AppointmentSummary>>> Handle(ListAppointmentsQuery request, CancellationToken cancellationToken)
    {
        // Overlap com a janela [FromUtc, ToUtc) — o mesmo teste usado por
        // TimeSlot.Overlaps, so que direto na query para o Postgres filtrar.
        var query = dbContext.Appointments.AsNoTracking()
            .Where(a => a.Slot.StartUtc < request.ToUtc && request.FromUtc < a.Slot.EndUtc);

        if (request.ResourceId is { } resourceId)
        {
            query = query.Where(a => a.ResourceId == resourceId);
        }

        var appointments = await query.OrderBy(a => a.Slot.StartUtc).ToListAsync(cancellationToken);

        IReadOnlyList<AppointmentSummary> items = appointments
            .Select(a => new AppointmentSummary(
                a.Id.Value, a.CustomerId, a.ResourceId, a.ServiceId, a.ServiceName,
                a.Slot.StartUtc, a.Slot.EndUtc, a.Status.ToString(), a.Price.Amount, a.Price.Currency, a.Notes))
            .ToList();

        return Result.Success(items);
    }
}
