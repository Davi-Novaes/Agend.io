namespace Agendio.Modules.Tenancy.Application.SetTenantBusinessHours;

public sealed record BusinessHoursEntryDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
