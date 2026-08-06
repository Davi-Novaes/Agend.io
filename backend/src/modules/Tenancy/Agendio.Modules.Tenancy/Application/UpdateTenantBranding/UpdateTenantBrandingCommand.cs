using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantBranding;

// Sem TenantId: vem de ITenantContext (claim do JWT), nao do corpo da
// requisicao — quem chama ja esta autenticado como dono DESTE tenant.
public sealed record UpdateTenantBrandingCommand(string PrimaryColorHex) : ICommand;
