using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Application.ResendConfirmationEmail;

public sealed record ResendConfirmationEmailCommand(Guid TenantId, string Email) : ICommand, IHasExplicitTenant;
