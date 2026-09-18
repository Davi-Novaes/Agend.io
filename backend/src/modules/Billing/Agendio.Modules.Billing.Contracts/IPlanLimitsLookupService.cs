using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Billing.Contracts;

/// <summary>null = sem limite (plano Gratis, "Padrao" legado, ou o proprio campo especifico do plano).</summary>
public sealed record PlanLimits(int? MaxUnits, int? MaxProfessionals, int? MaxCustomers);

/// <summary>
/// Unico ponto de leitura que Tenancy/Resources/Customers tem sobre Billing —
/// so pra checar limite de plano antes de criar unidade/profissional/cliente.
/// Retorna null (nao lanca, nao falha) quando o tenant nao tem assinatura/plano
/// resolvivel: uma falha de lookup de billing nunca deve travar quem ja usa o
/// produto — o chamador trata null como "sem restricao a aplicar".
/// </summary>
public interface IPlanLimitsLookupService
{
    Task<PlanLimits?> GetActivePlanLimitsAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
