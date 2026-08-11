using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.CreateUnit;

public sealed record CreateUnitCommand(string Name, string? Address) : ICommand<Guid>;
