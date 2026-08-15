namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Se e como o cliente final paga antes do agendamento ser confirmado (Fase 16).
/// Nunca "Full" separado de "Deposit": ajustar DepositPercentage para 100
/// produz cobranca integral sem precisar de um terceiro valor de enum.
/// </summary>
public enum PaymentRequirement
{
    None,
    Deposit,
}
