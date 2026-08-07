using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Catalog.Application.DeactivateService;

public sealed record SetServiceActiveStatusCommand(Guid ServiceId, bool IsActive) : ICommand;
