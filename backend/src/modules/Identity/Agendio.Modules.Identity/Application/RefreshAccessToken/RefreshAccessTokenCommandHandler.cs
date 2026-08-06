using System.Security.Claims;
using Agendio.Infrastructure.Multitenancy;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.RefreshAccessToken;

/// <summary>
/// Implementa rotacao de refresh token com deteccao de reuso: cada refresh
/// consome o token atual e emite um novo na MESMA familia. Se o token
/// apresentado JA estiver revogado, isso so acontece se alguem tiver capturado
/// um token que ja foi trocado — sinal de roubo — e a resposta e revogar TODOS
/// os tokens daquela familia, forcando novo login em todos os dispositivos.
/// </summary>
public sealed class RefreshAccessTokenCommandHandler(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    ITenantLookupService tenantLookupService,
    IJwtTokenService jwtTokenService,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : ICommandHandler<RefreshAccessTokenCommand, AuthTokensResult>
{
    private const int RefreshTokenLifetimeDays = 30;
    private static readonly Error InvalidTokenError = Error.Unauthorized("Auth.InvalidRefreshToken", "Sessao invalida ou expirada. Faca login novamente.");

    public async Task<Result<AuthTokensResult>> Handle(RefreshAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = refreshTokenGenerator.Hash(request.RefreshToken);

        // O tenant ainda nao e conhecido neste ponto (por isso IgnoreQueryFilters):
        // a busca e por um hash de 512 bits, alta entropia suficiente para servir
        // de chave de busca segura sem depender do filtro de tenant. A politica de
        // RLS da tabela refresh_tokens tem uma excecao documentada para esta unica
        // consulta (ver docs/adr e migration) — sempre por hash exato, nunca scan.
        var presentedToken = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (presentedToken is null)
        {
            return Result.Failure<AuthTokensResult>(InvalidTokenError);
        }

        // A partir daqui o tenant e conhecido: ancora no contexto para que todo
        // acesso subsequente (inclusive RLS) opere sobre o tenant certo.
        tenantContext.SetTenant(presentedToken.TenantId);

        if (presentedToken.RevokedAtUtc is not null)
        {
            await RevokeEntireFamilyAsync(presentedToken.FamilyId, cancellationToken);
            return Result.Failure<AuthTokensResult>(InvalidTokenError);
        }

        if (!presentedToken.IsActive(clock.UtcNow))
        {
            return Result.Failure<AuthTokensResult>(InvalidTokenError);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == presentedToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthTokensResult>(InvalidTokenError);
        }

        var tenant = await tenantLookupService.FindByIdAsync(presentedToken.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Result.Failure<AuthTokensResult>(InvalidTokenError);
        }

        var newRawToken = refreshTokenGenerator.GenerateToken();
        var newTokenHash = refreshTokenGenerator.Hash(newRawToken);
        var rotatedToken = presentedToken.Rotate(newTokenHash, clock.UtcNow, TimeSpan.FromDays(RefreshTokenLifetimeDays));

        presentedToken.Revoke(clock.UtcNow);
        dbContext.RefreshTokens.Add(rotatedToken);

        var claims = BuildClaims(user, tenant);
        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.GenerateAccessToken(claims);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokensResult(accessToken, accessTokenExpiresAtUtc, newRawToken, rotatedToken.ExpiresAtUtc));
    }

    private async Task RevokeEntireFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var familyTokens = await dbContext.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in familyTokens)
        {
            token.Revoke(clock.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
