using Agendio.Modules.Catalog.Contracts;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.GetAvailableSlots;

/// <summary>
/// Motor de disponibilidade minimo: horario de trabalho do recurso (Resources)
/// menos os agendamentos ja ocupados (Scheduling), em passos fixos. Nao ha
/// cache (Redis) nem granularidade configuravel por tenant ainda — o Sprint 3
/// deixou isso para quando o primeiro consumidor real (este) existisse.
/// </summary>
public sealed class GetAvailableSlotsQueryHandler(
    SchedulingDbContext dbContext,
    IClock clock,
    IResourceLookupService resourceLookup,
    IServiceLookupService serviceLookup,
    ITenantLookupService tenantLookup) : IQueryHandler<GetAvailableSlotsQuery, IReadOnlyList<AvailableSlot>>
{
    private const int SlotStepMinutes = 15;

    private static readonly AppointmentStatus[] ActiveStatuses =
    [
        AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.InProgress,
    ];

    public async Task<Result<IReadOnlyList<AvailableSlot>>> Handle(GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantLookup.FindByIdAsync(TenantId.From(request.TenantId), cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<IReadOnlyList<AvailableSlot>>(Error.NotFound("Availability.TenantNotFound", "Estabelecimento nao encontrado."));
        }

        var resource = await resourceLookup.FindByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null || !resource.IsActive)
        {
            return Result.Failure<IReadOnlyList<AvailableSlot>>(Error.NotFound("Availability.ResourceNotFound", "Recurso nao encontrado ou inativo."));
        }

        var service = await serviceLookup.FindByIdAsync(request.ServiceId, cancellationToken);
        if (service is null || !service.IsActive)
        {
            return Result.Failure<IReadOnlyList<AvailableSlot>>(Error.NotFound("Availability.ServiceNotFound", "Servico nao encontrado ou inativo."));
        }

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Result.Failure<IReadOnlyList<AvailableSlot>>(Error.Failure("Availability.InvalidTimeZone", "Fuso horario do estabelecimento invalido."));
        }

        var windows = resource.WorkingHours
            .Where(w => w.DayOfWeek == request.Date.DayOfWeek)
            .OrderBy(w => w.StartTime)
            .ToList();

        if (windows.Count == 0)
        {
            return Result.Success<IReadOnlyList<AvailableSlot>>([]);
        }

        var dayStartLocal = request.Date.ToDateTime(TimeOnly.MinValue);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, timeZone);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal.AddDays(1), timeZone);

        var occupied = await dbContext.Appointments.AsNoTracking()
            .Where(a => a.ResourceId == request.ResourceId
                && a.Slot.StartUtc < dayEndUtc && dayStartUtc < a.Slot.EndUtc
                && ActiveStatuses.Contains(a.Status))
            .Select(a => new { a.Slot.StartUtc, a.Slot.EndUtc })
            .ToListAsync(cancellationToken);

        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        var step = TimeSpan.FromMinutes(SlotStepMinutes);
        var nowUtc = clock.UtcNow;
        var slots = new List<AvailableSlot>();

        foreach (var window in windows)
        {
            var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(request.Date.ToDateTime(window.StartTime), timeZone);
            var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(request.Date.ToDateTime(window.EndTime), timeZone);

            for (var candidateStart = windowStartUtc; candidateStart + duration <= windowEndUtc; candidateStart += step)
            {
                if (candidateStart <= nowUtc)
                {
                    continue;
                }

                var candidateEnd = candidateStart + duration;
                var overlaps = occupied.Any(o => candidateStart < o.EndUtc && o.StartUtc < candidateEnd);

                if (!overlaps)
                {
                    slots.Add(new AvailableSlot(candidateStart, candidateEnd));
                }
            }
        }

        // Janelas de trabalho sobrepostas (configuradas por engano, ou vindas de
        // duas faixas que se tocam) nao podem gerar o mesmo horario duas vezes.
        IReadOnlyList<AvailableSlot> distinctSlots = slots
            .DistinctBy(s => s.StartUtc)
            .OrderBy(s => s.StartUtc)
            .ToList();

        return Result.Success(distinctSlots);
    }
}
