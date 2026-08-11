using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.CreateResource;

public sealed record CreateResourceCommand(string Name, ResourceType Type, int Capacity, string? Description, Guid? UnitId) : ICommand<Guid>;
