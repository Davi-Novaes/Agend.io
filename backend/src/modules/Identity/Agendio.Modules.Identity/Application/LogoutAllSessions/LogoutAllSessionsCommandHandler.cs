using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.LogoutAllSessions;

/// <summary>
/// "Sair de todos os dispositivos" acionavel pelo proprio usuario — ate aqui a
/// unica forma de revogar TODAS as sessoes de uma vez era o efeito colateral
/// automatico de ResetPassword/ChangePassword, ou a deteccao de reuso de
/// refresh token (RefreshAccessTokenCommandHandler). Sem senha nova nenhuma
/// aqui: so revoga (P1-6, docs/AUTH_BILLING_SECURITY_AUDIT.md).
/// </summary>
public sealed class LogoutAllSessionsCommandHandler(
    IdentityDbContext dbContext, IClock clock, SecurityAuditLogger<IdentityDbContext> securityAuditLogger)
    : ICommandHandler<LogoutAllSessionsCommand>
{
    public async Task<Result> Handle(LogoutAllSessionsCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        var activeTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(clock.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await securityAuditLogger.LogAsync("LogoutAllSessions", success: true, user.TenantId.Value, user.Id.Value, null, cancellationToken);

        return Result.Success();
    }
}
