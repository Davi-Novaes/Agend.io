using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.SetResourceServices;

public sealed record SetResourceServicesCommand(Guid ResourceId, IReadOnlyList<Guid> ServiceIds) : ICommand;
