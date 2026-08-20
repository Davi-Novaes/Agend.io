namespace Agendio.Infrastructure.Security;

/// <summary>Constantes compartilhadas entre quem emite o token de onboarding (Identity) e quem exige a policy (Billing).</summary>
public static class OnboardingAuthConstants
{
    public const string AuthenticationScheme = "Onboarding";

    /// <summary>Mesmo nome de claim usado por HttpTenantContext no scheme de tenant — conceito identico, autoridade de assinatura diferente.</summary>
    public const string TenantIdClaimType = "tenant_id";

    public const string AuthorizationPolicy = "OnboardingOnly";
}
