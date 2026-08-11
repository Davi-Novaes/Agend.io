using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.GetUnitById;

public sealed record GetUnitByIdQuery(Guid UnitId) : IQuery<UnitDetails>;

public sealed record UnitDetails(Guid Id, string Name, string? Address, bool IsActive);
