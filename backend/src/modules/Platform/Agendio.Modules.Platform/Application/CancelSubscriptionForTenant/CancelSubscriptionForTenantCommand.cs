using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.CancelSubscriptionForTenant;

public sealed record CancelSubscriptionForTenantCommand(Guid TenantId) : ICommand;
