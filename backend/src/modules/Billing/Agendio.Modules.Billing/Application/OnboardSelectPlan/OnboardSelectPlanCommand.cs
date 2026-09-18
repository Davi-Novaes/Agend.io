using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Billing.Application.OnboardSelectPlan;

// So registra a INTENCAO de plano — nunca ativa, nunca toca a Asaas (P1-5,
// docs/AUTH_BILLING_SECURITY_AUDIT.md). Ativacao de verdade acontece depois do
// login (que exige e-mail confirmado), via /subscription/activate-free ou
// /subscription/subscribe — os mesmos endpoints ja usados fora do onboarding.
public sealed record OnboardSelectPlanCommand(Guid TenantId, Guid PlanId)
    : ICommand<OnboardSelectPlanResult>, IHasExplicitTenant;

public sealed record OnboardSelectPlanResult(bool RequiresPayment);
