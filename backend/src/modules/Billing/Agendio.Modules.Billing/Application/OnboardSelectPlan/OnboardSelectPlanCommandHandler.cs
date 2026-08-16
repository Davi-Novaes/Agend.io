using Agendio.Infrastructure;
using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Billing.Application.OnboardSelectPlan;

/// <summary>
/// Anonimo (roda no onboarding, antes de existir JWT) — TenantId vem explicito
/// no corpo, ancorado pelo ExplicitTenantBehavior via IHasExplicitTenant.
/// Free ativa direto, sem tocar a Asaas. Pago cria so o Checkout (link pra
/// pagina hospedada) — a Subscription continua Trialing ate o webhook
/// confirmar o primeiro pagamento (ver ProcessAsaasWebhookCommandHandler).
/// </summary>
public sealed class OnboardSelectPlanCommandHandler(
    BillingDbContext dbContext, ITenantContext tenantContext, IClock clock, IAsaasClient asaasClient,
    IOptions<FrontendOptions> frontendOptions)
    : ICommandHandler<OnboardSelectPlanCommand, OnboardSelectPlanResult>
{
    public async Task<Result<OnboardSelectPlanResult>> Handle(OnboardSelectPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await dbContext.Plans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == PlanId.From(request.PlanId) && p.IsActive, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<OnboardSelectPlanResult>(Error.NotFound("Plan.NotFound", "Plano nao encontrado ou inativo."));
        }

        var subscription = await SubscriptionProvisioning.FindOrCreateAsync(dbContext, tenantContext.TenantId, clock, cancellationToken);

        if (subscription.Status is SubscriptionStatus.Active)
        {
            return Result.Failure<OnboardSelectPlanResult>(
                Error.Conflict("Subscription.AlreadyActive", "Este estabelecimento ja tem uma assinatura ativa."));
        }

        if (plan.Id == PlanConfiguration.FreePlanId)
        {
            var activateResult = subscription.ActivateAsFree(PlanConfiguration.FreePlanId);
            if (activateResult.IsFailure)
            {
                return Result.Failure<OnboardSelectPlanResult>(activateResult.Error);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new OnboardSelectPlanResult(RequiresPayment: false, CheckoutLink: null));
        }

        // Cobra a partir do fim do trial (nao imediatamente) — mesmo raciocinio
        // de SubscribeToPlanCommandHandler: ninguem perde os dias de trial.
        var todayUtc = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var trialEndDate = DateOnly.FromDateTime(subscription.TrialEndsAtUtc.UtcDateTime);
        var nextDueDate = trialEndDate > todayUtc ? trialEndDate : todayUtc;

        // Mesma pagina neutra pro sucesso e cancelamento — o polling do
        // frontend (nao o redirect) e quem decide quando liberar o proximo passo.
        var returnUrl = $"{frontendOptions.Value.BaseUrl}/onboarding/checkout-retorno";

        var checkout = await asaasClient.CreateCreditCardCheckoutAsync(
            plan.Name, "Assinatura mensal Agendio", plan.PriceAmount, nextDueDate,
            returnUrl, returnUrl, request.TenantId.ToString(), cancellationToken);

        return Result.Success(new OnboardSelectPlanResult(RequiresPayment: true, CheckoutLink: checkout.Link));
    }
}
