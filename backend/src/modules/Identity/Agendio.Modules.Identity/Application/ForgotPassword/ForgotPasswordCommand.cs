using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Application.ForgotPassword;

public sealed record ForgotPasswordCommand(Guid TenantId, string Email) : ICommand, IHasExplicitTenant;
