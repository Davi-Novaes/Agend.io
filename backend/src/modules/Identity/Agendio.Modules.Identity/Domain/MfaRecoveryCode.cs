using Agendio.Modules.Identity.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Identity.Domain;

/// <summary>
/// Codigo de recuperacao de MFA, uso unico. Mesmo molde de RefreshToken (1:muitos
/// com User, hash SHA-256 reaproveitando IRefreshTokenGenerator.Hash) — ver ADR
/// 0008. Tabela dedicada em vez de array jsonb no User: ganha RLS e trilha de
/// auditoria de graca, sem race de leitura-modificacao-escrita num jsonb
/// compartilhado quando varios codigos sao gerados/consumidos.
/// </summary>
public sealed class MfaRecoveryCode : Entity<MfaRecoveryCodeId>, ITenantOwned
{
    public TenantId TenantId { get; private set; } = null!;

    public UserId UserId { get; private set; } = null!;

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UsedAtUtc { get; private set; }

    private MfaRecoveryCode()
    {
    }

    private MfaRecoveryCode(TenantId tenantId, UserId userId, string codeHash, DateTimeOffset createdAtUtc)
        : base(MfaRecoveryCodeId.New())
    {
        TenantId = tenantId;
        UserId = userId;
        CodeHash = codeHash;
        CreatedAtUtc = createdAtUtc;
    }

    public static MfaRecoveryCode Create(TenantId tenantId, UserId userId, string codeHash, DateTimeOffset nowUtc) =>
        new(tenantId, userId, codeHash, nowUtc);

    public void MarkUsed(DateTimeOffset nowUtc) => UsedAtUtc ??= nowUtc;
}
