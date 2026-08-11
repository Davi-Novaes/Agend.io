namespace Agendio.Modules.Resources.Contracts;

public sealed record WorkingHourLookup(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record ResourceLookupResult(
    Guid ResourceId, string Name, string Type, int Capacity, bool IsActive, IReadOnlyList<WorkingHourLookup> WorkingHours, Guid? UnitId);
