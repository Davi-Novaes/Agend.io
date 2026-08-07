using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.SetTenantActiveStatus;

public sealed record SetTenantActiveStatusCommand(Guid TenantId, bool IsActive) : ICommand;
