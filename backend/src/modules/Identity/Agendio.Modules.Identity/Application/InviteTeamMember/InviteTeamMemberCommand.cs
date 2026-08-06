using Agendio.Modules.Identity.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.InviteTeamMember;

// Sem TenantId: vem de ITenantContext (claim do JWT) — quem convida ja esta
// autenticado como Owner DESTE tenant (ver RequireAuthorization no endpoint).
public sealed record InviteTeamMemberCommand(string Email, UserRole Role) : ICommand<InviteTeamMemberResult>;

public sealed record InviteTeamMemberResult(Guid InvitationId, string Token, DateTimeOffset ExpiresAtUtc);
