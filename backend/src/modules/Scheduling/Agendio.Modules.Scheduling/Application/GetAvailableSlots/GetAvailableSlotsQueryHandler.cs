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
/// Motor de disponibilidade (Fase 4): horario de trabalho do recurso
/// (Resources) intersectado com o horario de funcionamento do estabelecimento
/// (Tenancy, quando configurado), menos folgas do recurso, datas fechadas do
/// estabelecimento e os agendamentos ja ocupados (mais o intervalo entre
/// agendamentos, se configurado) — em passos fixos. Nao ha cache (Redis) nem
/// granularidade configuravel por tenant ainda.
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

    private static readonly IReadOnlyList<AvailableSlot> NoSlots = [];

    public async Task<Result<IReadOnlyList<AvailableSlot>>> Handle(GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantLookup.GetAvailabilityInfoAsync(TenantId.From(request.TenantId), cancellationToken);
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

        // Data fechada (feriado/evento) — o estabelecimento inteiro nao atende, independente do recurso.
        if (tenant.ClosedDates.Contains(request.Date))
        {
            return Result.Success(NoSlots);
        }

        // Folga do recurso — TimeOff e granularidade de dia inteiro (ver Domain/TimeOff.cs).
        var timeOff = await resourceLookup.ListTimeOffAsync(request.ResourceId, request.Date, request.Date, cancellationToken);
        if (timeOff.Count > 0)
        {
            return Result.Success(NoSlots);
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

        var effectiveWindows = ComputeEffectiveWindows(resource.WorkingHours, tenant.BusinessHours, request.Date.DayOfWeek);
        if (effectiveWindows.Count == 0)
        {
            return Result.Success(NoSlots);
        }

        var dayStartLocal = request.Date.ToDateTime(TimeOnly.MinValue);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, timeZone);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal.AddDays(1), timeZone);

        // Janela um pouco mais ampla que o dia: um agendamento do dia anterior
        // pode "vazar" o intervalo (buffer) para dentro do dia pedido.
        var buffer = TimeSpan.FromMinutes(tenant.AppointmentBufferMinutes);
        var occupied = await dbContext.Appointments.AsNoTracking()
            .Where(a => a.ResourceId == request.ResourceId
                && a.Slot.StartUtc < dayEndUtc + buffer && dayStartUtc - buffer < a.Slot.EndUtc
                && ActiveStatuses.Contains(a.Status))
            .Select(a => new { a.Slot.StartUtc, a.Slot.EndUtc })
            .ToListAsync(cancellationToken);

        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        var step = TimeSpan.FromMinutes(SlotStepMinutes);
        var nowUtc = clock.UtcNow;
        var slots = new List<AvailableSlot>();

        foreach (var window in effectiveWindows)
        {
            var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(request.Date.ToDateTime(window.Start), timeZone);
            var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(request.Date.ToDateTime(window.End), timeZone);

            for (var candidateStart = windowStartUtc; candidateStart + duration <= windowEndUtc; candidateStart += step)
            {
                if (candidateStart <= nowUtc)
                {
                    continue;
                }

                var candidateEnd = candidateStart + duration;
                // O intervalo entre agendamentos vale nos dois sentidos: nem
                // comecar cedo demais depois de um atendimento anterior, nem
                // terminar tarde demais em cima do proximo.
                var overlaps = occupied.Any(o => candidateStart < o.EndUtc + buffer && o.StartUtc - buffer < candidateEnd);

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

    /// <summary>
    /// Intersecta o horario do recurso com o horario de funcionamento do
    /// estabelecimento para o dia pedido. Sem BusinessHours configurado
    /// (lista vazia — nunca preenchida pelo dono), nao restringe nada, so o
    /// horario do recurso vale (zero-config: Fase 1 tornou isso opcional).
    /// Com BusinessHours configurado mas sem entrada para o dia pedido, o
    /// estabelecimento esta fechado nesse dia, mesmo que o recurso tenha
    /// horario configurado.
    /// </summary>
    private static List<(TimeOnly Start, TimeOnly End)> ComputeEffectiveWindows(
        IReadOnlyList<WorkingHourLookup> resourceWorkingHours, IReadOnlyList<BusinessHoursLookup> tenantBusinessHours, DayOfWeek dayOfWeek)
    {
        var resourceWindows = resourceWorkingHours.Where(w => w.DayOfWeek == dayOfWeek).OrderBy(w => w.StartTime).ToList();
        if (resourceWindows.Count == 0)
        {
            return [];
        }

        if (tenantBusinessHours.Count == 0)
        {
            return resourceWindows.Select(w => (w.StartTime, w.EndTime)).ToList();
        }

        var businessWindows = tenantBusinessHours.Where(b => b.DayOfWeek == dayOfWeek).ToList();
        if (businessWindows.Count == 0)
        {
            return [];
        }

        var effective = new List<(TimeOnly Start, TimeOnly End)>();
        foreach (var resourceWindow in resourceWindows)
        {
            foreach (var businessWindow in businessWindows)
            {
                var start = resourceWindow.StartTime > businessWindow.StartTime ? resourceWindow.StartTime : businessWindow.StartTime;
                var end = resourceWindow.EndTime < businessWindow.EndTime ? resourceWindow.EndTime : businessWindow.EndTime;

                if (start < end)
                {
                    effective.Add((start, end));
                }
            }
        }

        return effective;
    }
}
