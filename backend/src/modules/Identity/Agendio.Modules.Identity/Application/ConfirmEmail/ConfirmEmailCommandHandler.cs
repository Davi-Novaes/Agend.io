using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.ConfirmEmail;

/// <summary>
/// O tenant so e conhecido DEPOIS de localizar o usuario pelo hash do token —
/// mesmo padrao (e mesma excecao de RLS) do AcceptInvitationCommandHandler.
/// </summary>
public sealed class ConfirmEmailCommandHandler(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    IRefreshTokenGenerator tokenGenerator,
    IClock clock) : ICommandHandler<ConfirmEmailCommand>
{
    private static readonly Error InvalidTokenError =
        Error.Unauthorized("Auth.EmailConfirmationTokenInvalid", "Token de confirmacao invalido ou expirado.");

    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.Hash(request.Token);

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.EmailConfirmationTokenHash == tokenHash, cancellationToken);

        if (user is null)
        {
            return Result.Failure(InvalidTokenError);
        }

        tenantContext.SetTenant(user.TenantId);

        var confirmResult = user.ConfirmEmail(clock.UtcNow);
        if (confirmResult.IsFailure)
        {
            return confirmResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
