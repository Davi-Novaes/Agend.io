using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.GetAppointmentDeposit;

/// <summary>Null quando o agendamento nao tem sinal associado (tenant nao exige pagamento) — nao e erro.</summary>
public sealed record GetAppointmentDepositQuery(Guid AppointmentId) : IQuery<AppointmentDepositSummary?>;

public sealed record AppointmentDepositSummary(
    decimal Amount, string Currency, string Status, string? InvoiceUrl, DateTimeOffset? PaidAtUtc);
