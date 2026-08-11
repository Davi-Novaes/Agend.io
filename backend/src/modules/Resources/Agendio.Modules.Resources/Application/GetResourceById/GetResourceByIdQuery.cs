using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.GetResourceById;

public sealed record GetResourceByIdQuery(Guid ResourceId) : IQuery<ResourceDetails>;

public sealed record WorkingHourEntryDetails(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record ResourceDetails(
    Guid Id,
    string Name,
    ResourceType Type,
    int Capacity,
    string? Description,
    bool IsActive,
    Guid? UnitId,
    IReadOnlyList<WorkingHourEntryDetails> WorkingHours);
