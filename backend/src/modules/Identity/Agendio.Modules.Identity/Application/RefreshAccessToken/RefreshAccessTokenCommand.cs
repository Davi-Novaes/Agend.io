using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.RefreshAccessToken;

/// <summary>
/// Nao implementa IHasExplicitTenant: o TenantId so e conhecido DEPOIS de
/// localizar o registro pelo hash do token (ver RefreshAccessTokenCommandHandler).
/// </summary>
public sealed record RefreshAccessTokenCommand(string RefreshToken) : ICommand<AuthTokensResult>;
