namespace Agendio.Modules.Catalog.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que outro modulo tem sobre Catalog (ver regra
/// de dependencia em CLAUDE.md). O tenant e resolvido de forma ambiente pelo
/// global query filter do CatalogDbContext — nao precisa ser passado aqui.
/// </summary>
public interface IServiceLookupService
{
    Task<ServiceLookupResult?> FindByIdAsync(Guid serviceId, CancellationToken cancellationToken = default);
}
