using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantBanner;

// Sem TenantId: vem de ITenantContext, como em UpdateTenantLogoCommand.
public sealed record UpdateTenantBannerCommand(byte[] Content, string ContentType) : ICommand<string>;
