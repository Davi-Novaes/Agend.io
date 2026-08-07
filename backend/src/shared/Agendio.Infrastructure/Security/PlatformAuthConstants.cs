namespace Agendio.Infrastructure.Security;

/// <summary>Constantes compartilhadas entre o modulo Platform (que emite o claim) e Agendio.Api (que exige o claim na policy de autorizacao).</summary>
public static class PlatformAuthConstants
{
    public const string AuthenticationScheme = "Platform";

    public const string ScopeClaimType = "scope";

    public const string PlatformScopeValue = "platform";

    public const string AuthorizationPolicy = "PlatformOnly";
}
