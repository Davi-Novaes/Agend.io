using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantProfile;

// Sem TenantId: vem de ITenantContext (claim do JWT), nao do corpo da
// requisicao — quem chama ja esta autenticado como dono DESTE tenant.
public sealed record UpdateTenantProfileCommand(
    string? Description, string? Phone, string? WhatsApp, string? Email, string? Address, string? InstagramUrl, string? FacebookUrl)
    : ICommand;
