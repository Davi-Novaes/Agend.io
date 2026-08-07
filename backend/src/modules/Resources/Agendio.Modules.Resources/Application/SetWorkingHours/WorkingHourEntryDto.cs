namespace Agendio.Modules.Resources.Application.SetWorkingHours;

public sealed record WorkingHourEntryDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
