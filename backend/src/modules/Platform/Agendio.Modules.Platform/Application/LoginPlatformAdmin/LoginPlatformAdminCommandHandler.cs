using System.Security.Claims;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Infrastructure.Mfa;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Application.LoginPlatformAdmin;

public sealed class LoginPlatformAdminCommandHandler(
    PlatformDbContext dbContext,
    IPasswordHasher passwordHasher,
    IPlatformJwtTokenService jwtTokenService,
    IPlatformMfaChallengeStore mfaChallengeStore,
    IRefreshTokenGenerator challengeTokenGenerator,
    SecurityAuditLogger<PlatformDbContext> securityAuditLogger,
    IClock clock) : ICommandHandler<LoginPlatformAdminCommand, LoginPlatformAdminOutcome>
{
    private const int MfaChallengeLifetimeMinutes = 5;
    private static readonly Error InvalidCredentialsError = Error.Unauthorized("Platform.InvalidCredentials", "E-mail ou senha invalidos.");

    public async Task<Result<LoginPlatformAdminOutcome>> Handle(LoginPlatformAdminCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var admin = await dbContext.PlatformAdmins.SingleOrDefaultAsync(a => a.Email == normalizedEmail, cancellationToken);

        if (admin is null || !admin.IsActive)
        {
            await LogAsync("LoginFailed", success: false, actorId: null, "admin-not-found-or-inactive", cancellationToken);
            return Result.Failure<LoginPlatformAdminOutcome>(InvalidCredentialsError);
        }

        // Mesmo raciocinio de LoginCommandHandler (Identity): conta trancada
        // devolve a MESMA mensagem generica, nunca "conta bloqueada" — e pula a
        // verificacao de senha, sem somar mais tentativas (a conta ja esta contada).
        if (admin.IsLockedOut(clock.UtcNow))
        {
            await LogAsync("LoginFailed", success: false, admin.Id.Value, "account-locked", cancellationToken);
            return Result.Failure<LoginPlatformAdminOutcome>(InvalidCredentialsError);
        }

        if (!passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            admin.RegisterFailedLoginAttempt(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            var lockedNow = admin.IsLockedOut(clock.UtcNow);
            await LogAsync(lockedNow ? "AccountLocked" : "LoginFailed", success: false, admin.Id.Value, "wrong-password", cancellationToken);

            return Result.Failure<LoginPlatformAdminOutcome>(InvalidCredentialsError);
        }

        admin.RegisterSuccessfulLogin();
        await dbContext.SaveChangesAsync(cancellationToken);

        if (admin.MfaEnabled)
        {
            var challengeToken = challengeTokenGenerator.GenerateToken();
            var challengeExpiresAtUtc = clock.UtcNow.AddMinutes(MfaChallengeLifetimeMinutes);

            await mfaChallengeStore.CreateAsync(challengeToken, admin.Id, challengeExpiresAtUtc, cancellationToken);
            await LogAsync("LoginMfaChallengeIssued", success: true, admin.Id.Value, null, cancellationToken);

            return Result.Success<LoginPlatformAdminOutcome>(new LoginPlatformAdminMfaChallenge(challengeToken, challengeExpiresAtUtc));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.Value.ToString()),
            new(ClaimTypes.Email, admin.Email),
            new(PlatformAuthConstants.ScopeClaimType, PlatformAuthConstants.PlatformScopeValue),
        };

        var (accessToken, expiresAtUtc) = jwtTokenService.GenerateAccessToken(claims);
        await LogAsync("LoginSucceeded", success: true, admin.Id.Value, null, cancellationToken);

        return Result.Success<LoginPlatformAdminOutcome>(new LoginPlatformAdminSuccess(new LoginPlatformAdminResult(accessToken, expiresAtUtc, admin.FullName)));
    }

    private Task LogAsync(string eventType, bool success, Guid? actorId, string? reason, CancellationToken cancellationToken) =>
        securityAuditLogger.LogAsync(eventType, success, tenantId: null, actorId, reason is null ? null : $"{{\"reason\":\"{reason}\"}}", cancellationToken);
}
