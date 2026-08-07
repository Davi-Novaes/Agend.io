using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Billing.Infrastructure.Jobs;

/// <summary>
/// Roda diariamente (ver registro em Program.cs). Desativa o tenant (via
/// ITenantAdministrationService — o mesmo mecanismo que ja bloqueia login desde
/// o Sprint 6) quando o trial de 14 dias venceu sem nenhuma assinatura ativa, ou
/// quando uma assinatura ficou PastDue alem da carencia.
///
/// Nenhuma ancora de ITenantContext e necessaria: nem Subscription/Payment nem
/// Tenant tem filtro de tenant pra brigar (ver comentario em Subscription.cs).
/// </summary>
public sealed class BillingReconciliationJob(
    BillingDbContext dbContext, ITenantAdministrationService tenantAdministrationService, IClock clock,
    ILogger<BillingReconciliationJob> logger)
{
    // Boleto pode levar 1-3 dias uteis pra compensar depois de pago — 5 dias e
    // um valor de bom senso, nao config: ajustar depois e trocar esta constante.
    private const int GracePeriodDays = 5;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var pastDueCutoff = now.AddDays(-GracePeriodDays);

        var overdueSubscriptions = await dbContext.Subscriptions
            .Where(s =>
                (s.Status == SubscriptionStatus.Trialing && s.TrialEndsAtUtc < now) ||
                // (CurrentPeriodEndsAtUtc ?? TrialEndsAtUtc): cobre o caso do
                // PRIMEIRO pagamento de uma assinatura vencer sem nunca ter
                // sido confirmado — nesse caso CurrentPeriodEndsAtUtc ainda
                // nunca foi setado por MarkActive.
                (s.Status == SubscriptionStatus.PastDue && (s.CurrentPeriodEndsAtUtc ?? s.TrialEndsAtUtc) < pastDueCutoff))
            .ToListAsync(cancellationToken);

        foreach (var subscription in overdueSubscriptions)
        {
            var result = await tenantAdministrationService.SetActiveStatusAsync(subscription.TenantId, isActive: false, cancellationToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Falha ao desativar tenant {TenantId} por assinatura vencida: {Error}",
                    subscription.TenantId.Value, result.Error.Message);
                continue;
            }

            logger.LogInformation(
                "Tenant {TenantId} desativado pelo job de conciliacao (trial/assinatura vencidos sem pagamento).",
                subscription.TenantId.Value);
        }
    }
}
