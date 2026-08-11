namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que outro modulo tem sobre Unit (ver regra de
/// dependencia em CLAUDE.md). Usado por Resources para validar, ao criar/editar
/// um recurso, que o UnitId recebido existe e pertence ao tenant atual — o
/// global query filter do TenancyDbContext ja restringe ao tenant corrente, entao
/// "existe" aqui ja implica "e deste tenant".
/// </summary>
public interface IUnitLookupService
{
    Task<bool> ExistsAsync(Guid unitId, CancellationToken cancellationToken = default);
}
