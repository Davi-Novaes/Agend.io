using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.SetUnitBusinessHours;

public sealed record UnitBusinessHoursEntryDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record SetUnitBusinessHoursCommand(Guid UnitId, IReadOnlyList<UnitBusinessHoursEntryDto> Entries) : ICommand;
