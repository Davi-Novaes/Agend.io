using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.CreateUnit;

public sealed record CreateUnitCommand(string Name, string? Address, string? City, string? State, string? Country) : ICommand<Guid>;
