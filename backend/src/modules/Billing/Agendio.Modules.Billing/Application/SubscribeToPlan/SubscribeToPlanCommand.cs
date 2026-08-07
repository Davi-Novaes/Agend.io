using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Billing.Application.SubscribeToPlan;

// Nome/CPF-CNPJ/e-mail vem direto do formulario da tela de cobranca (nao do
// onboarding, que fica intocado) — e o unico lugar que precisa desses dados,
// evita acoplar Billing a Identity so pra ler nome/e-mail do usuario logado.
// CPF/CNPJ nunca e persistido nas nossas tabelas, so passa em transito pra Asaas.
public sealed record SubscribeToPlanCommand(Guid PlanId, string FullName, string CpfCnpj, string? Email) : ICommand<SubscribeToPlanResult>;

public sealed record SubscribeToPlanResult(string InvoiceUrl);
