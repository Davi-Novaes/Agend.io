using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.ResetPassword;

/// <summary>
/// O tenant so e conhecido DEPOIS de localizar o usuario pelo hash do token —
/// mesmo padrao (e mesma excecao de RLS) de ConfirmEmailCommandHandler. Ao
/// concluir, revoga TODOS os refresh tokens do usuario (todas as familias, nao
/// so uma) — quem redefiniu a senha por nao lembrar dela quer, por definicao,
/// derrubar qualquer sessao que ainda esteja usando a senha antiga.
/// </summary>
public sealed class ResetPasswordCommandHandler(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    IRefreshTokenGenerator tokenGenerator,
    IPasswordHasher passwordHasher,
    IClock clock,
    SecurityAuditLogger<IdentityDbContext> securityAuditLogger) : ICommandHandler<ResetPasswordCommand>
{
    private static readonly Error InvalidTokenError =
        Error.Unauthorized("Auth.PasswordResetTokenInvalid", "Token de recuperacao invalido ou expirado.");

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.Hash(request.Token);

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash, cancellationToken);

        if (user is null)
        {
            return Result.Failure(InvalidTokenError);
        }

        tenantContext.SetTenant(user.TenantId);

        var consumeResult = user.ConsumePasswordResetToken(clock.UtcNow);
        if (consumeResult.IsFailure)
        {
            await securityAuditLogger.LogAsync("PasswordResetCompleted", success: false, user.TenantId.Value, user.Id.Value, null, cancellationToken);
            return consumeResult;
        }

        var newPasswordHash = passwordHasher.Hash(request.NewPassword);
        var resetResult = user.ResetPassword(newPasswordHash, clock.UtcNow);
        if (resetResult.IsFailure)
        {
            return resetResult;
        }

        var activeTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(clock.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await securityAuditLogger.LogAsync("PasswordResetCompleted", success: true, user.TenantId.Value, user.Id.Value, null, cancellationToken);

        return Result.Success();
    }
}
