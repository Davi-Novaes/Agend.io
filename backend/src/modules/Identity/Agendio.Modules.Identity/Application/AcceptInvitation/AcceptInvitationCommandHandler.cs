using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.AcceptInvitation;

/// <summary>
/// O tenant so e conhecido DEPOIS de localizar o convite pelo hash do token —
/// mesmo padrao (e mesma excecao de RLS) do RefreshAccessTokenCommandHandler.
/// </summary>
public sealed class AcceptInvitationCommandHandler(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    IRefreshTokenGenerator tokenGenerator,
    IPasswordHasher passwordHasher,
    IClock clock) : ICommandHandler<AcceptInvitationCommand, Guid>
{
    private static readonly Error InvalidInvitationError =
        Error.Unauthorized("TeamInvitation.Invalid", "Convite invalido, ja utilizado ou expirado.");

    public async Task<Result<Guid>> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.Hash(request.Token);

        var invitation = await dbContext.TeamInvitations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null)
        {
            return Result.Failure<Guid>(InvalidInvitationError);
        }

        tenantContext.SetTenant(invitation.TenantId);

        if (!invitation.IsPending(clock.UtcNow))
        {
            return Result.Failure<Guid>(InvalidInvitationError);
        }

        var emailAlreadyMember = await dbContext.Users.AnyAsync(u => u.Email == invitation.Email, cancellationToken);
        if (emailAlreadyMember)
        {
            return Result.Failure<Guid>(
                Error.Conflict("TeamInvitation.AlreadyMember", "Este e-mail ja pertence a equipe deste estabelecimento."));
        }

        var passwordHash = passwordHasher.Hash(request.Password);
        var userResult = User.Register(
            invitation.TenantId, invitation.Email, request.FullName, passwordHash, invitation.Role, emailConfirmedAtUtc: clock.UtcNow);
        if (userResult.IsFailure)
        {
            return Result.Failure<Guid>(userResult.Error);
        }

        invitation.Accept(clock.UtcNow);
        dbContext.Users.Add(userResult.Value);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(userResult.Value.Id.Value);
    }
}
