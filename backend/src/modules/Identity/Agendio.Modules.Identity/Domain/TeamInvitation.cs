using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Identity.Domain;

/// <summary>
/// Convite pendente para um novo membro da equipe. Entidade simples (nao
/// AggregateRoot), no mesmo espirito de RefreshToken: o token e localizado por
/// hash ANTES de o tenant ser conhecido (link publico enviado por e-mail), a
/// mesma excecao de RLS documentada em RefreshTokenConfiguration se aplica aqui.
/// </summary>
public sealed class TeamInvitation : Entity<TeamInvitationId>, ITenantOwned
{
    public TenantId TenantId { get; private set; } = null!;

    public Email Email { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    private TeamInvitation()
    {
    }

    private TeamInvitation(TenantId tenantId, Email email, UserRole role, string tokenHash, DateTimeOffset nowUtc, DateTimeOffset expiresAtUtc)
        : base(TeamInvitationId.New())
    {
        TenantId = tenantId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static TeamInvitation Create(TenantId tenantId, Email email, UserRole role, string tokenHash, DateTimeOffset nowUtc, TimeSpan lifetime) =>
        new(tenantId, email, role, tokenHash, nowUtc, nowUtc.Add(lifetime));

    public bool IsPending(DateTimeOffset nowUtc) => AcceptedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Accept(DateTimeOffset nowUtc) => AcceptedAtUtc ??= nowUtc;
}
