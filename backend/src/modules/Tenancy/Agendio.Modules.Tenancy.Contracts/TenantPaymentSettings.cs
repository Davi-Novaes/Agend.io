namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>
/// Se o agendamento publico exige pagamento de sinal e qual percentual — lido
/// pelo Scheduling ao criar um agendamento publico (Fase 16). Desligado por
/// padrao (PaymentRequired = false), Scheduling nao pede CPF nem gera cobranca.
/// </summary>
public sealed record TenantPaymentSettings(bool PaymentRequired, int DepositPercentage);
