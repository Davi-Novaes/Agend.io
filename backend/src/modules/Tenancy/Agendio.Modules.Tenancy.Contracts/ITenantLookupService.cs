using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que outro modulo tem sobre Tenancy — a forma
/// permitida de consultar dado de outro modulo (ver regra de dependencia em
/// CLAUDE.md). Nunca expõe o agregado Tenant nem o DbContext de Tenancy.
/// </summary>
public interface ITenantLookupService
{
    Task<TenantLookupResult?> FindByIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
