using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateUnit;

public sealed record UpdateUnitCommand(Guid UnitId, string Name, string? Address) : ICommand;
