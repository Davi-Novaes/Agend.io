namespace Agendio.Modules.Resources.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que outro modulo tem sobre Resources (ver
/// regra de dependencia em CLAUDE.md). O tenant e resolvido de forma ambiente
/// pelo global query filter do ResourcesDbContext — nao precisa ser passado aqui.
/// </summary>
public interface IResourceLookupService
{
    Task<ResourceLookupResult?> FindByIdAsync(Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>Recursos ATIVOS de um tipo (ex.: "Person") — usado por Financeiro para listar profissionais elegiveis a regra de comissao.</summary>
    Task<IReadOnlyList<ResourceLookupResult>> ListActiveByTypeAsync(string type, CancellationToken cancellationToken = default);
}
