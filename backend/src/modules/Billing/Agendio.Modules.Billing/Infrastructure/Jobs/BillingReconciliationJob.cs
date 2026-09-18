using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
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
                // Plano Free ativa direto (ActivateAsFree) sem passar por Trialing/
                // PastDue e nunca cobra — mas e defesa em profundidade explicita
                // aqui, nao so uma consequencia acidental do fluxo normal.
                s.PlanId != PlanConfiguration.FreePlanId &&
                ((s.Status == SubscriptionStatus.Trialing && s.TrialEndsAtUtc < now) ||
                // (CurrentPeriodEndsAtUtc ?? TrialEndsAtUtc): cobre o caso do
                // PRIMEIRO pagamento de uma assinatura vencer sem nunca ter
                // sido confirmado — nesse caso CurrentPeriodEndsAtUtc ainda
                // nunca foi setado por MarkActive.
                (s.Status == SubscriptionStatus.PastDue && (s.CurrentPeriodEndsAtUtc ?? s.TrialEndsAtUtc) < pastDueCutoff)))
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

        // Cancelamento com carencia (Subscription.Cancel, pedido explicito do
        // usuario 2026-09-05): quem cancelou um plano pago continua Active/
        // PastDue ate o periodo ja pago vencer -- aqui e onde o corte de
        // verdade acontece, batendo CurrentPeriodEndsAtUtc contra agora.
        var pendingCancellations = await dbContext.Subscriptions
            .Where(s =>
                s.CanceledAtUtc != null &&
                s.Status != SubscriptionStatus.Canceled &&
                s.CurrentPeriodEndsAtUtc != null &&
                s.CurrentPeriodEndsAtUtc < now)
            .ToListAsync(cancellationToken);

        foreach (var subscription in pendingCancellations)
        {
            // Sem SetActiveStatusAsync aqui de proposito: o cancelamento
            // IMEDIATO (CancelSubscriptionCommandHandler, Trialing ou sem
            // periodo pago) tambem nunca desativa o tenant -- so troca
            // Status pra Canceled e deixa o gate de billing (AppLayout,
            // "status !== Active") bloquear o painel. Mesmo comportamento
            // aqui, so que adiado ate o periodo pago vencer.
            subscription.FinalizeCancellation();

            logger.LogInformation(
                "Assinatura do tenant {TenantId} finalizada pelo job de conciliacao (cancelamento com carencia venceu).",
                subscription.TenantId.Value);
        }

        if (pendingCancellations.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
