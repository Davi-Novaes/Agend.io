using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.InviteTeamMember;

public sealed class InviteTeamMemberCommandHandler(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    // Mesmo algoritmo do refresh token (segredo de alta entropia -> hash
    // SHA-256): reaproveitar em vez de duplicar uma interface identica.
    IRefreshTokenGenerator tokenGenerator,
    IClock clock) : ICommandHandler<InviteTeamMemberCommand, InviteTeamMemberResult>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public async Task<Result<InviteTeamMemberResult>> Handle(InviteTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<InviteTeamMemberResult>(emailResult.Error);
        }

        var emailAlreadyMember = await dbContext.Users.AnyAsync(u => u.Email == emailResult.Value, cancellationToken);
        if (emailAlreadyMember)
        {
            return Result.Failure<InviteTeamMemberResult>(
                Error.Conflict("TeamInvitation.AlreadyMember", "Este e-mail ja pertence a equipe deste estabelecimento."));
        }

        var now = clock.UtcNow;
        var hasPendingInvitation = await dbContext.TeamInvitations
            .AnyAsync(i => i.Email == emailResult.Value && i.AcceptedAtUtc == null && i.ExpiresAtUtc > now, cancellationToken);
        if (hasPendingInvitation)
        {
            return Result.Failure<InviteTeamMemberResult>(
                Error.Conflict("TeamInvitation.AlreadyPending", "Ja existe um convite pendente para este e-mail."));
        }

        var rawToken = tokenGenerator.GenerateToken();
        var invitation = TeamInvitation.Create(
            tenantContext.TenantId, emailResult.Value, request.Role, tokenGenerator.Hash(rawToken), now, InvitationLifetime);

        dbContext.TeamInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new InviteTeamMemberResult(invitation.Id.Value, rawToken, invitation.ExpiresAtUtc));
    }
}
