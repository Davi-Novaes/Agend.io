using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.UpdateResource;

public sealed record UpdateResourceCommand(Guid ResourceId, string Name, ResourceType Type, int Capacity, string? Description) : ICommand;
