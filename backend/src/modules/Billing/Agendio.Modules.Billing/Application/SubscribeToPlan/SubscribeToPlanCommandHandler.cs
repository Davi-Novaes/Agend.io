using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Application.SubscribeToPlan;

public sealed class SubscribeToPlanCommandHandler(
    BillingDbContext dbContext, ITenantContext tenantContext, IClock clock, IAsaasClient asaasClient)
    : ICommandHandler<SubscribeToPlanCommand, SubscribeToPlanResult>
{
    public async Task<Result<SubscribeToPlanResult>> Handle(SubscribeToPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await dbContext.Plans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == PlanId.From(request.PlanId) && p.IsActive, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<SubscribeToPlanResult>(Error.NotFound("Plan.NotFound", "Plano nao encontrado ou inativo."));
        }

        var subscription = await SubscriptionProvisioning.FindOrCreateAsync(dbContext, tenantContext.TenantId, clock, cancellationToken);

        if (subscription.Status is SubscriptionStatus.Active)
        {
            return Result.Failure<SubscribeToPlanResult>(
                Error.Conflict("Subscription.AlreadyActive", "Este estabelecimento ja tem uma assinatura ativa."));
        }

        // Se ja existe cliente na Asaas (ex.: reassinando depois de cancelar),
        // reaproveita — nunca cria um segundo cliente pro mesmo tenant.
        var asaasCustomerId = subscription.AsaasCustomerId
            ?? await asaasClient.CreateCustomerAsync(request.FullName, request.CpfCnpj, request.Email, cancellationToken);

        // Cobra a partir do fim do trial (nao imediatamente) se o dono assinar
        // durante o periodo gratis — ninguem perde os dias de trial restantes.
        var todayUtc = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var trialEndDate = DateOnly.FromDateTime(subscription.TrialEndsAtUtc.UtcDateTime);
        var nextDueDate = trialEndDate > todayUtc ? trialEndDate : todayUtc;

        var asaasSubscription = await asaasClient.CreateSubscriptionAsync(asaasCustomerId, plan.PriceAmount, nextDueDate, cancellationToken);

        var attachResult = subscription.AttachAsaasCheckout(asaasCustomerId, asaasSubscription.AsaasSubscriptionId);
        if (attachResult.IsFailure)
        {
            return Result.Failure<SubscribeToPlanResult>(attachResult.Error);
        }

        dbContext.Payments.Add(new Payment(
            tenantContext.TenantId, subscription.Id, asaasSubscription.AsaasPaymentId,
            plan.PriceAmount, asaasSubscription.DueDate, asaasSubscription.InvoiceUrl, asaasSubscription.BillingType));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubscribeToPlanResult(asaasSubscription.InvoiceUrl ?? string.Empty));
    }
}
