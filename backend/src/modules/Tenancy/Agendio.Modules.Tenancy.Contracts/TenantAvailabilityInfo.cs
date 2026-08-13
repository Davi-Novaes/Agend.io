namespace Agendio.Modules.Tenancy.Contracts;

public sealed record BusinessHoursLookup(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>Dados de que o motor de disponibilidade (Scheduling) precisa sobre o tenant — ver GetAvailableSlotsQueryHandler.</summary>
public sealed record TenantAvailabilityInfo(
    string TimeZoneId,
    IReadOnlyList<BusinessHoursLookup> BusinessHours,
    IReadOnlyList<DateOnly> ClosedDates,
    int AppointmentBufferMinutes);
