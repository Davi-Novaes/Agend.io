using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Billing.Contracts;

/// <summary>
/// Unico ponto de leitura/escrita sincrona que o modulo Platform tem sobre
/// Billing — mesmo desenho de ITenantAdministrationService (Tenancy.Contracts):
/// cross-tenant explicito, para o painel Super Admin monitorar e administrar
/// assinaturas. CancelSubscriptionForTenantAsync e excecao deliberada a
/// "Contracts so expõe leitura sincrona" pelo mesmo motivo de SetActiveStatusAsync
/// (Tenancy.Contracts): cancelar a assinatura de um tenant especifico a pedido
/// do Super Admin e uma orquestracao sincrona genuina (chama a Asaas de
/// verdade), nao um caso para integration event.
/// </summary>
public interface IBillingAdministrationService
{
    Task<IReadOnlyList<SubscriptionSummary>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<Result> CancelSubscriptionForTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
