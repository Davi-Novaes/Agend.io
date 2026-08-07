namespace Agendio.Modules.Billing.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que o modulo Platform tem sobre Billing —
/// mesmo desenho de ITenantAdministrationService (Tenancy.Contracts): leitura
/// cross-tenant explicita, para o painel Super Admin monitorar assinaturas.
/// </summary>
public interface IBillingAdministrationService
{
    Task<IReadOnlyList<SubscriptionSummary>> ListAllAsync(CancellationToken cancellationToken = default);
}
