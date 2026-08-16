using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Application.GetOnboardingSubscriptionStatus;

/// <summary>
/// Usado pelo onboarding pra fazer polling enquanto a aba de checkout da
/// Asaas esta aberta. Active e o sinal real de "pronto" nos dois caminhos:
/// Free ativa direto (ActivateAsFree); pago so chega em Active quando o
/// webhook confirmar o primeiro pagamento (ProcessAsaasWebhookCommandHandler).
/// </summary>
public sealed class GetOnboardingSubscriptionStatusQueryHandler(BillingDbContext dbContext, ITenantContext tenantContext)
    : IQueryHandler<GetOnboardingSubscriptionStatusQuery, OnboardingSubscriptionStatusResult>
{
    public async Task<Result<OnboardingSubscriptionStatusResult>> Handle(
        GetOnboardingSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == tenantContext.TenantId, cancellationToken);

        return Result.Success(new OnboardingSubscriptionStatusResult(subscription?.Status is SubscriptionStatus.Active));
    }
}
