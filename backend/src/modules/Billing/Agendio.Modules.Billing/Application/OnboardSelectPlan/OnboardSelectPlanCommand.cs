using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Billing.Application.OnboardSelectPlan;

// Sem FullName/CpfCnpj/Email de proposito: o Checkout da Asaas coleta nome,
// CPF/CNPJ, telefone e endereco do pagador na propria pagina hospedada quando
// nao passamos "customer"/"customerData" — contrato validado direto na
// sandbox (Fase 24). Passar dado PARCIAL do titular e rejeitado pela Asaas
// ("tudo ou nada"), entao omitir por completo e a escolha certa aqui — evita
// pedir CPF/CNPJ no onboarding, que nesse ponto so tem e-mail/senha do dono.
public sealed record OnboardSelectPlanCommand(Guid TenantId, Guid PlanId)
    : ICommand<OnboardSelectPlanResult>, IHasExplicitTenant;

public sealed record OnboardSelectPlanResult(bool RequiresPayment, string? CheckoutLink);
