using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.Logout;

/// <summary>
/// Nao implementa IHasExplicitTenant pelo mesmo motivo de RefreshAccessTokenCommand:
/// o TenantId so e conhecido depois de localizar o registro pelo hash do token.
/// Sempre responde sucesso (ver LogoutCommandHandler) — nao ha nada de sensivel
/// a esconder aqui, e o endpoint limpa o cookie independente do resultado.
/// </summary>
public sealed record LogoutCommand(string RefreshToken) : ICommand;
