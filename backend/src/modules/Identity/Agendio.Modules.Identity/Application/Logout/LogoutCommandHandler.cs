using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.Logout;

/// <summary>
/// Revoga so o refresh token apresentado (a sessao/dispositivo atual), nao a
/// familia inteira — isso e reservado para deteccao de reuso (roubo), ver
/// RefreshAccessTokenCommandHandler. Logout normal nao deveria derrubar as
/// outras abas/dispositivos onde o usuario esta logado.
/// </summary>
public sealed class LogoutCommandHandler(
    IdentityDbContext dbContext, ITenantContext tenantContext, IRefreshTokenGenerator refreshTokenGenerator, IClock clock)
    : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = refreshTokenGenerator.Hash(request.RefreshToken);

        // Mesma excecao de RLS documentada em RefreshAccessTokenCommandHandler:
        // busca por hash de 512 bits antes do tenant ser conhecido.
        var presentedToken = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (presentedToken is not null && presentedToken.RevokedAtUtc is null)
        {
            tenantContext.SetTenant(presentedToken.TenantId);
            presentedToken.Revoke(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
