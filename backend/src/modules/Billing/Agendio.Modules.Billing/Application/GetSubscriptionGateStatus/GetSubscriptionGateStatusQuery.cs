using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Billing.Application.GetSubscriptionGateStatus;

// Separada de GetMySubscriptionQuery de proposito: aquela traz plano/valor/nota
// fiscal (dado de billing, so Owner ve — ver BillingEndpoints), mas TODO usuario
// autenticado do tenant precisa saber se a assinatura esta paga pra o app shell
// decidir se bloqueia o resto do sistema ate a assinatura ser regularizada
// (AppLayout.requiresPaymentBeforeAccess) — sem isto, Staff simplesmente nunca
// seria bloqueado (a query cheia agora exige Owner e falharia com 403 pra ele).
public sealed record GetSubscriptionGateStatusQuery : IQuery<SubscriptionGateStatusResult>;

public sealed record SubscriptionGateStatusResult(string Status);
