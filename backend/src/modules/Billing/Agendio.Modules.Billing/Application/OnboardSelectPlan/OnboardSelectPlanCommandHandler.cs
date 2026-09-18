using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Application.OnboardSelectPlan;

/// <summary>
/// Anonimo (roda no onboarding, antes de existir JWT de tenant) — TenantId vem
/// explicito no corpo, ancorado pelo ExplicitTenantBehavior via IHasExplicitTenant.
///
/// So grava a intencao de plano (Subscription.SelectPlan) — nao ativa Free nem
/// cria Checkout na Asaas aqui. O e-mail do dono ainda nao foi confirmado
/// neste ponto (confirma-lo e o proximo passo do onboarding), e a ativacao de
/// verdade so acontece apos o login — que ja exige e-mail confirmado
/// (LoginCommandHandler) — via /subscription/activate-free ou /subscription/subscribe.
/// Isso fecha o "conta ativa antes do e-mail confirmado" sem precisar checar
/// estado do Identity daqui (P1-5, docs/AUTH_BILLING_SECURITY_AUDIT.md).
/// </summary>
public sealed class OnboardSelectPlanCommandHandler(BillingDbContext dbContext, ITenantContext tenantContext, IClock clock)
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

        var selectResult = subscription.SelectPlan(plan.Id);
        if (selectResult.IsFailure)
        {
            return Result.Failure<OnboardSelectPlanResult>(selectResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new OnboardSelectPlanResult(RequiresPayment: plan.Id != PlanConfiguration.FreePlanId));
    }
}
