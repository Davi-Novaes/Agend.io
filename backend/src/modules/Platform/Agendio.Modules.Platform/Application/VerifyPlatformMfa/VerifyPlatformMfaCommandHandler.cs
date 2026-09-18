using System.Security.Claims;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Application.LoginPlatformAdmin;
using Agendio.Modules.Platform.Infrastructure.Mfa;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Application.VerifyPlatformMfa;

public sealed class VerifyPlatformMfaCommandHandler(
    PlatformDbContext dbContext,
    IPlatformMfaChallengeStore mfaChallengeStore,
    IPlatformMfaCodeVerifier mfaCodeVerifier,
    IPlatformJwtTokenService jwtTokenService,
    SecurityAuditLogger<PlatformDbContext> securityAuditLogger) : ICommandHandler<VerifyPlatformMfaCommand, LoginPlatformAdminResult>
{
    private static readonly Error InvalidChallengeError =
        Error.Unauthorized("Auth.InvalidMfaChallenge", "Sessao de verificacao invalida ou expirada. Faca login novamente.");

    private static readonly Error InvalidCodeError = Error.Unauthorized("Auth.InvalidMfaCode", "Codigo invalido.");

    public async Task<Result<LoginPlatformAdminResult>> Handle(VerifyPlatformMfaCommand request, CancellationToken cancellationToken)
    {
        // Uso unico — mesmo raciocinio de VerifyMfaCommandHandler (Identity).
        var challenge = await mfaChallengeStore.ConsumeAsync(request.ChallengeToken, cancellationToken);
        if (challenge is null)
        {
            return Result.Failure<LoginPlatformAdminResult>(InvalidChallengeError);
        }

        var admin = await dbContext.PlatformAdmins.SingleOrDefaultAsync(a => a.Id == challenge.AdminId, cancellationToken);
        if (admin is null || !admin.IsActive || !admin.MfaEnabled)
        {
            return Result.Failure<LoginPlatformAdminResult>(InvalidChallengeError);
        }

        if (!mfaCodeVerifier.Verify(admin, request.Code))
        {
            await securityAuditLogger.LogAsync("LoginFailed", success: false, tenantId: null, admin.Id.Value, "{\"reason\":\"invalid-mfa-code\"}", cancellationToken);
            return Result.Failure<LoginPlatformAdminResult>(InvalidCodeError);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.Value.ToString()),
            new(ClaimTypes.Email, admin.Email),
            new(PlatformAuthConstants.ScopeClaimType, PlatformAuthConstants.PlatformScopeValue),
        };

        var (accessToken, expiresAtUtc) = jwtTokenService.GenerateAccessToken(claims);
        await securityAuditLogger.LogAsync("LoginSucceeded", success: true, tenantId: null, admin.Id.Value, null, cancellationToken);

        return Result.Success(new LoginPlatformAdminResult(accessToken, expiresAtUtc, admin.FullName));
    }
}
