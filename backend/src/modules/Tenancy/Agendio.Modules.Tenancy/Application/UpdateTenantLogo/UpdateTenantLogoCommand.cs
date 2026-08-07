using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantLogo;

// Sem TenantId: vem de ITenantContext, como em UpdateTenantBrandingCommand.
public sealed record UpdateTenantLogoCommand(byte[] Content, string ContentType) : ICommand<string>;
