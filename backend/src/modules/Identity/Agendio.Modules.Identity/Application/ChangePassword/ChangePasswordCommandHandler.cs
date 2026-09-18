using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.ChangePassword;

/// <summary>
/// Troca autenticada (usuario ja logado, sabe a senha atual) — ao contrario de
/// ResetPassword (token por e-mail, presume que a senha foi esquecida), aqui a
/// pessoa prova posse da conta apresentando a senha atual. Mesmo efeito
/// colateral de seguranca: revoga TODOS os refresh tokens do usuario, forcando
/// login novo em qualquer outro dispositivo/aba — inclusive o que fez esta
/// propria chamada, que so continua valido ate o access token (15 min) expirar.
/// </summary>
public sealed class ChangePasswordCommandHandler(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IClock clock,
    SecurityAuditLogger<IdentityDbContext> securityAuditLogger) : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            await securityAuditLogger.LogAsync("PasswordChanged", success: false, user.TenantId.Value, user.Id.Value, null, cancellationToken);
            return Result.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Senha atual invalida."));
        }

        var newPasswordHash = passwordHasher.Hash(request.NewPassword);
        var changeResult = user.ResetPassword(newPasswordHash, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return changeResult;
        }

        var activeTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(clock.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await securityAuditLogger.LogAsync("PasswordChanged", success: true, user.TenantId.Value, user.Id.Value, null, cancellationToken);

        return Result.Success();
    }
}
