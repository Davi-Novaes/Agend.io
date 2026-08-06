using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Infrastructure.Multitenancy;

/// <summary>
/// ITenantContext para cenarios sem requisicao HTTP: as design-time factories do
/// EF Core (`dotnet ef migrations`/`database update`) usam isto, e futuramente
/// jobs em background que processam varios tenants tambem podem — nesses casos
/// o TenantId e ancorado explicitamente via SetTenant antes de cada operacao,
/// nao lido de um HttpContext que nao existe fora de uma requisicao web.
/// </summary>
public sealed class NullTenantContext : ITenantContext
{
    private TenantId? _tenantId;
    private string? _tenantSlug;

    public bool HasTenant => _tenantId is not null;

    public TenantId TenantId => _tenantId
        ?? throw new InvalidOperationException("Nenhum tenant foi ancorado neste contexto. Chame SetTenant antes de usar.");

    public string? TenantSlug => _tenantSlug;

    public void SetTenant(TenantId tenantId, string? tenantSlug = null)
    {
        _tenantId = tenantId;
        _tenantSlug = tenantSlug;
    }
}
