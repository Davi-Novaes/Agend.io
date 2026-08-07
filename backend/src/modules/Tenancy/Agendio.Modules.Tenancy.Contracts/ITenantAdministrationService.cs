using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>
/// Excecao deliberada e explicita a "Contracts so expõe leitura sincrona" (ver o
/// mesmo raciocinio em ICustomerRegistrationService): o painel Super Admin
/// (modulo Platform) precisa listar TODOS os tenants — inclusive inativos, o que
/// ITenantLookupService nunca faria — e precisar ativar/desativar um tenant e
/// uma orquestracao sincrona genuina, nao um caso para integration event.
/// Nenhum outro modulo alem de Platform deveria consumir esta interface.
/// </summary>
public interface ITenantAdministrationService
{
    Task<IReadOnlyList<TenantLookupResult>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<Result> SetActiveStatusAsync(TenantId tenantId, bool isActive, CancellationToken cancellationToken = default);
}
