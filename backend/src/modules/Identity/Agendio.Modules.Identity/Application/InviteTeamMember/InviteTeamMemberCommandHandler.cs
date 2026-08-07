using Agendio.Infrastructure;
using Agendio.Infrastructure.Notifications;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Identity.Application.InviteTeamMember;

public sealed class InviteTeamMemberCommandHandler(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    ITenantLookupService tenantLookupService,
    // Mesmo algoritmo do refresh token (segredo de alta entropia -> hash
    // SHA-256): reaproveitar em vez de duplicar uma interface identica.
    IRefreshTokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    IClock clock,
    ILogger<InviteTeamMemberCommandHandler> logger) : ICommandHandler<InviteTeamMemberCommand, InviteTeamMemberResult>
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

        await TrySendInvitationEmailAsync(emailResult.Value.Value, rawToken, cancellationToken);

        return Result.Success(new InviteTeamMemberResult(invitation.Id.Value, rawToken, invitation.ExpiresAtUtc));
    }

    // Falha de e-mail nao derruba o convite: o link continua valido e volta na
    // resposta (ver InviteTeamMemberResult) para o dono compartilhar na mao —
    // e por isso que so logamos, sem propagar a excecao.
    private async Task TrySendInvitationEmailAsync(string toAddress, string rawToken, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await tenantLookupService.FindByIdAsync(tenantContext.TenantId, cancellationToken);
            var tenantName = tenant?.Name ?? "seu estabelecimento";
            var inviteUrl = $"{frontendOptions.Value.BaseUrl}/invitations/{rawToken}";

            var html = $"""
                <p>Voce foi convidado para fazer parte da equipe de <strong>{tenantName}</strong> no Agendio.</p>
                <p><a href="{inviteUrl}">Clique aqui para criar sua conta</a></p>
                <p>Este link expira em 7 dias.</p>
                """;

            await emailSender.SendAsync(toAddress, $"Convite para {tenantName} no Agendio", html, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar e-mail de convite de equipe. O link continua valido.");
        }
    }
}
