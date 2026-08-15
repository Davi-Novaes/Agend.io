using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>
/// Sinal/pagamento antecipado exigido pelo tenant no agendamento publico (Fase 16).
/// Nasce Pending assim que o agendamento e criado; a cobranca em si (GatewayChargeId/
/// InvoiceUrl) pode ficar em branco se a chamada ao gateway falhar no momento —
/// o agendamento continua valido, so sem link de pagamento ainda (a equipe
/// acompanha e pode gerar manualmente depois). Paid so via webhook do gateway.
/// </summary>
public sealed class AppointmentDeposit : AggregateRoot<AppointmentDepositId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public AppointmentId AppointmentId { get; private set; } = null!;

    public Money Amount { get; private set; } = null!;

    public AppointmentDepositStatus Status { get; private set; }

    public string? GatewayChargeId { get; private set; }

    public string? InvoiceUrl { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private AppointmentDeposit()
    {
    }

    private AppointmentDeposit(TenantId tenantId, AppointmentId appointmentId, Money amount)
        : base(AppointmentDepositId.New())
    {
        TenantId = tenantId;
        AppointmentId = appointmentId;
        Amount = amount;
        Status = AppointmentDepositStatus.Pending;
    }

    public static Result<AppointmentDeposit> Create(TenantId tenantId, AppointmentId appointmentId, Money amount)
    {
        if (amount.Amount <= 0)
        {
            return Result.Failure<AppointmentDeposit>(Error.Validation("AppointmentDeposit.InvalidAmount", "O valor do sinal deve ser maior que zero."));
        }

        return Result.Success(new AppointmentDeposit(tenantId, appointmentId, amount));
    }

    public Result AttachGatewayCharge(string gatewayChargeId, string invoiceUrl)
    {
        if (Status != AppointmentDepositStatus.Pending)
        {
            return Result.Failure(Error.Validation("AppointmentDeposit.InvalidTransition", "So e possivel anexar cobranca a um sinal pendente."));
        }

        GatewayChargeId = gatewayChargeId;
        InvoiceUrl = invoiceUrl;
        return Result.Success();
    }

    public Result MarkPaid(DateTimeOffset paidAtUtc)
    {
        if (Status == AppointmentDepositStatus.Paid)
        {
            return Result.Success();
        }

        if (Status != AppointmentDepositStatus.Pending)
        {
            return Result.Failure(Error.Validation("AppointmentDeposit.InvalidTransition", "So e possivel confirmar pagamento de um sinal pendente."));
        }

        Status = AppointmentDepositStatus.Paid;
        PaidAtUtc = paidAtUtc;
        return Result.Success();
    }

    public Result MarkFailed()
    {
        if (Status != AppointmentDepositStatus.Pending)
        {
            return Result.Failure(Error.Validation("AppointmentDeposit.InvalidTransition", "So e possivel marcar falha em um sinal pendente."));
        }

        Status = AppointmentDepositStatus.Failed;
        return Result.Success();
    }
}
