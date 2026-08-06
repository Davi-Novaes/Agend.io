using Agendio.Modules.Identity.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Identity.Domain;

/// <summary>
/// FamilyId agrupa toda a cadeia de tokens nascida de um mesmo login. Rotacionar
/// (trocar o refresh por um par novo de tokens) cria um RefreshToken novo com o
/// MESMO FamilyId e revoga o anterior.
///
/// Deteccao de reuso: se um token JA REVOGADO for apresentado de novo — sinal
/// classico de roubo/replay de um token antigo — a familia INTEIRA e revogada
/// (ver RevokeUserTokenFamilyCommand), forcando novo login em todos os
/// dispositivos daquela sessao. E por isso que aqui e uma entidade simples (nao
/// AggregateRoot): quem orquestra a revogacao em cascata e o Application layer,
/// que enxerga todos os tokens da familia, nao um unico agregado isolado.
/// </summary>
public sealed class RefreshToken : Entity<RefreshTokenId>, ITenantOwned
{
    public TenantId TenantId { get; private set; } = null!;

    public UserId UserId { get; private set; } = null!;

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(TenantId tenantId, UserId userId, Guid familyId, string tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
        : base(RefreshTokenId.New())
    {
        TenantId = tenantId;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static RefreshToken IssueNewFamily(TenantId tenantId, UserId userId, string tokenHash, DateTimeOffset nowUtc, TimeSpan lifetime) =>
        new(tenantId, userId, Guid.NewGuid(), tokenHash, nowUtc, nowUtc.Add(lifetime));

    /// <summary>Novo token na MESMA familia — o elo que permite revogar a cadeia inteira em caso de reuso.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset nowUtc, TimeSpan lifetime) =>
        new(TenantId, UserId, FamilyId, newTokenHash, nowUtc, nowUtc.Add(lifetime));

    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc ??= nowUtc;
}
