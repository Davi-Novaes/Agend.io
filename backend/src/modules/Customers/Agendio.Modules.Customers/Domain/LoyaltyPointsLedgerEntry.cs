using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Customers.Domain;

/// <summary>
/// Log imutavel de credito/resgate de pontos de fidelidade — nunca editado ou
/// apagado (mesmo raciocinio de StockMovement no Estoque e NotificationLogEntry
/// no Scheduling). AppointmentId so e preenchido em lancamentos Earned
/// originados de um AppointmentCompleted — e a chave do indice unico parcial
/// (tenant_id, appointment_id) WHERE kind = 'Earned' que garante idempotencia
/// contra redelivery do RabbitMQ (mesmo padrao de AccountReceivable/
/// AccountPayable.SourceAppointmentId no Financeiro).
/// </summary>
public sealed class LoyaltyPointsLedgerEntry : AggregateRoot<LoyaltyPointsLedgerEntryId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public CustomerId CustomerId { get; private set; } = null!;

    public LoyaltyPointsLedgerEntryKind Kind { get; private set; }

    public int Points { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private LoyaltyPointsLedgerEntry()
    {
    }

    private LoyaltyPointsLedgerEntry(
        TenantId tenantId, CustomerId customerId, LoyaltyPointsLedgerEntryKind kind, int points, Guid? appointmentId, DateTimeOffset occurredAtUtc)
        : base(LoyaltyPointsLedgerEntryId.New())
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Kind = kind;
        Points = points;
        AppointmentId = appointmentId;
        OccurredAtUtc = occurredAtUtc;
    }

    public static Result<LoyaltyPointsLedgerEntry> RecordEarned(
        TenantId tenantId, CustomerId customerId, int points, Guid appointmentId, DateTimeOffset occurredAtUtc)
    {
        if (points <= 0)
        {
            return Result.Failure<LoyaltyPointsLedgerEntry>(
                Error.Validation("LoyaltyPointsLedgerEntry.InvalidPoints", "A quantidade de pontos deve ser maior que zero."));
        }

        return Result.Success(new LoyaltyPointsLedgerEntry(tenantId, customerId, LoyaltyPointsLedgerEntryKind.Earned, points, appointmentId, occurredAtUtc));
    }

    public static Result<LoyaltyPointsLedgerEntry> RecordRedeemed(
        TenantId tenantId, CustomerId customerId, int points, DateTimeOffset occurredAtUtc)
    {
        if (points <= 0)
        {
            return Result.Failure<LoyaltyPointsLedgerEntry>(
                Error.Validation("LoyaltyPointsLedgerEntry.InvalidPoints", "A quantidade de pontos deve ser maior que zero."));
        }

        return Result.Success(new LoyaltyPointsLedgerEntry(tenantId, customerId, LoyaltyPointsLedgerEntryKind.Redeemed, points, appointmentId: null, occurredAtUtc));
    }
}
