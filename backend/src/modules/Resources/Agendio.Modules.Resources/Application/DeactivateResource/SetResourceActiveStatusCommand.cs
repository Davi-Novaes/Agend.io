using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.DeactivateResource;

public sealed record SetResourceActiveStatusCommand(Guid ResourceId, bool IsActive) : ICommand;
