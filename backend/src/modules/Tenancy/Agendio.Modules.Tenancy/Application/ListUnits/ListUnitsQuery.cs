using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.ListUnits;

public sealed record ListUnitsQuery : IQuery<IReadOnlyList<UnitSummary>>;

public sealed record UnitBusinessHoursSummary(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record UnitSummary(
    Guid Id, string Name, string? Address, string? City, string? State, string? Country,
    string? Phone, string? WhatsApp, bool IsActive, IReadOnlyList<UnitBusinessHoursSummary> BusinessHours);
