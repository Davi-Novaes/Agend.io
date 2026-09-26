using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.GetUnitById;

public sealed record GetUnitByIdQuery(Guid UnitId) : IQuery<UnitDetails>;

public sealed record UnitBusinessHoursDetails(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record UnitDetails(
    Guid Id, string Name, string? Address, string? City, string? State, string? Country,
    string? Phone, string? WhatsApp, bool IsActive, IReadOnlyList<UnitBusinessHoursDetails> BusinessHours);
