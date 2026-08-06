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
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.Login;

public sealed class LoginCommandHandler(
    IdentityDbContext dbContext,
    ITenantLookupService tenantLookupService,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : ICommandHandler<LoginCommand, AuthTokensResult>
{
    private const int RefreshTokenLifetimeDays = 30;

    public async Task<Result<AuthTokensResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenant = await tenantLookupService.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Result.Failure<AuthTokensResult>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado ou inativo."));
        }

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<AuthTokensResult>(Error.Unauthorized("Auth.InvalidCredentials", "E-mail ou senha invalidos."));
        }

        // ExplicitTenantBehavior ja ancorou o tenant no ITenantContext: o Global
        // Query Filter ja restringe esta busca ao tenant certo.
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == emailResult.Value, cancellationToken);

        // Mensagem identica para "nao existe" e "senha errada" — nao vazamos
        // se um e-mail esta cadastrado (evita enumeracao de contas).
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthTokensResult>(Error.Unauthorized("Auth.InvalidCredentials", "E-mail ou senha invalidos."));
        }

        var accessTokenClaims = BuildClaims(user, tenant);
        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.GenerateAccessToken(accessTokenClaims);

        var rawRefreshToken = refreshTokenGenerator.GenerateToken();
        var refreshTokenHash = refreshTokenGenerator.Hash(rawRefreshToken);
        var refreshTokenLifetime = TimeSpan.FromDays(RefreshTokenLifetimeDays);

        var refreshToken = RefreshToken.IssueNewFamily(tenantId, user.Id, refreshTokenHash, clock.UtcNow, refreshTokenLifetime);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokensResult(accessToken, accessTokenExpiresAtUtc, rawRefreshToken, refreshToken.ExpiresAtUtc));
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
