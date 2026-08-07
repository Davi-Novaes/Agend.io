using System.Security.Claims;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Application.LoginPlatformAdmin;

public sealed class LoginPlatformAdminCommandHandler(
    PlatformDbContext dbContext, IPasswordHasher passwordHasher, IPlatformJwtTokenService jwtTokenService)
    : ICommandHandler<LoginPlatformAdminCommand, LoginPlatformAdminResult>
{
    public async Task<Result<LoginPlatformAdminResult>> Handle(LoginPlatformAdminCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var admin = await dbContext.PlatformAdmins.SingleOrDefaultAsync(a => a.Email == normalizedEmail, cancellationToken);

        // Mensagem identica para "nao existe" e "senha errada" — mesmo raciocinio
        // de LoginCommandHandler (Identity): nao vazar se um e-mail existe.
        if (admin is null || !admin.IsActive || !passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            return Result.Failure<LoginPlatformAdminResult>(Error.Unauthorized("Platform.InvalidCredentials", "E-mail ou senha invalidos."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.Value.ToString()),
            new(ClaimTypes.Email, admin.Email),
            new(PlatformAuthConstants.ScopeClaimType, PlatformAuthConstants.PlatformScopeValue),
        };

        var (accessToken, expiresAtUtc) = jwtTokenService.GenerateAccessToken(claims);

        return Result.Success(new LoginPlatformAdminResult(accessToken, expiresAtUtc, admin.FullName));
    }
}
