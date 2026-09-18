using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
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

        // O validator ja garantiu formato valido (digito verificador) — aqui so
        // normaliza pra digitos puros antes de mandar pra Asaas, mesmo que o
        // usuario tenha digitado com pontuacao.
        var normalizedCpfCnpj = CpfCnpj.Create(request.CpfCnpj).Value.Value;

        // Se ja existe cliente na Asaas (ex.: reassinando depois de cancelar),
        // reaproveita — nunca cria um segundo cliente pro mesmo tenant.
        var asaasCustomerId = subscription.AsaasCustomerId
            ?? await asaasClient.CreateCustomerAsync(request.FullName, normalizedCpfCnpj, request.Email, cancellationToken);

        // Cobra a partir do fim do trial (nao imediatamente) se o dono assinar
        // durante o periodo gratis — ninguem perde os dias de trial restantes.
        var todayUtc = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var trialEndDate = DateOnly.FromDateTime(subscription.TrialEndsAtUtc.UtcDateTime);
        var nextDueDate = trialEndDate > todayUtc ? trialEndDate : todayUtc;

        // Reenviar o formulario antes de pagar (F5, duplo clique, voltar do
        // checkout) nao pode criar uma segunda assinatura recorrente na Asaas
        // — bug real encontrado em sandbox: o mesmo cliente acumulou 2
        // assinaturas ACTIVE simultaneas de R$99/mes. Se ja existe uma
        // assinatura Asaas vinculada, so recupera a fatura mais recente dela.
        var asaasSubscription = subscription.AsaasSubscriptionId is { } existingAsaasSubscriptionId
            ? await asaasClient.GetLatestSubscriptionPaymentAsync(existingAsaasSubscriptionId, cancellationToken)
            : await asaasClient.CreateSubscriptionAsync(asaasCustomerId, plan.PriceAmount, nextDueDate, cancellationToken);

        var attachResult = subscription.AttachAsaasCheckout(asaasCustomerId, asaasSubscription.AsaasSubscriptionId);
        if (attachResult.IsFailure)
        {
            return Result.Failure<SubscribeToPlanResult>(attachResult.Error);
        }

        // Mesmo motivo: sem essa checagem, reenviar o formulario tentaria
        // inserir outra linha com o mesmo AsaasPaymentId (indice unico),
        // derrubando a requisicao com erro 500 em vez de so devolver o link.
        var paymentAlreadyRecorded = await dbContext.Payments
            .AnyAsync(p => p.AsaasPaymentId == asaasSubscription.AsaasPaymentId, cancellationToken);
        if (!paymentAlreadyRecorded)
        {
            dbContext.Payments.Add(new Payment(
                tenantContext.TenantId, subscription.Id, asaasSubscription.AsaasPaymentId,
                plan.PriceAmount, asaasSubscription.DueDate, asaasSubscription.InvoiceUrl, asaasSubscription.BillingType));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubscribeToPlanResult(asaasSubscription.InvoiceUrl ?? string.Empty));
    }
}
