using System.Security.Claims;
using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Identity.Application;

/// <summary>
/// "Emitir tokens finais para um usuario ja autenticado" e compartilhado por dois
/// fluxos: login sem MFA (direto) e VerifyMfaCommandHandler (depois do codigo TOTP
/// confirmar). Extraido para nao duplicar claims/JWT/rotacao de refresh token nos
/// dois lugares — ver LoginCommandHandler e VerifyMfaCommandHandler.
/// </summary>
public sealed class AuthTokenIssuer(
    IdentityDbContext dbContext, IJwtTokenService jwtTokenService, IRefreshTokenGenerator refreshTokenGenerator, IClock clock)
{
    private const int RefreshTokenLifetimeDays = 30;

    public async Task<AuthTokensResult> IssueAsync(User user, TenantLookupResult tenant, CancellationToken cancellationToken)
    {
        var claims = BuildClaims(user, tenant);
        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.GenerateAccessToken(claims);

        var rawRefreshToken = refreshTokenGenerator.GenerateToken();
        var refreshTokenHash = refreshTokenGenerator.Hash(rawRefreshToken);
        var refreshTokenLifetime = TimeSpan.FromDays(RefreshTokenLifetimeDays);

        var refreshToken = RefreshToken.IssueNewFamily(user.TenantId, user.Id, refreshTokenHash, clock.UtcNow, refreshTokenLifetime);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokensResult(accessToken, accessTokenExpiresAtUtc, rawRefreshToken, refreshToken.ExpiresAtUtc);
    }

    private static List<Claim> BuildClaims(User user, TenantLookupResult tenant) =>
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
        new Claim(ClaimTypes.Email, user.Email.Value),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim(HttpTenantContext.TenantIdClaimType, tenant.TenantId.Value.ToString()),
        new Claim(HttpTenantContext.TenantSlugClaimType, tenant.Slug),
    ];
}
