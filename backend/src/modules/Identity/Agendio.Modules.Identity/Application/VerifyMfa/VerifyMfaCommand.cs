using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.VerifyMfa;

/// <summary>
/// Nao implementa IHasExplicitTenant: o TenantId so e conhecido depois de
/// resolver o challenge token no IMfaChallengeStore (mesmo raciocinio de
/// RefreshAccessTokenCommand — ver RefreshAccessTokenCommandHandler/ADR-0002).
/// </summary>
public sealed record VerifyMfaCommand(string ChallengeToken, string Code) : ICommand<AuthTokensResult>;
