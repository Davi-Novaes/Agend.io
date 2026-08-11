using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.SetUnitActiveStatus;

public sealed record SetUnitActiveStatusCommand(Guid UnitId, bool IsActive) : ICommand;
