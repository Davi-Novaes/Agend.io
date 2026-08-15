using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>
/// Fase 14 — registro imutavel de um cancelamento ou remarcacao, com o motivo
/// informado por quem fez a mudanca — nunca editado ou apagado depois de criado
/// (mesmo raciocinio de NotificationLogEntry/StockMovement: log de auditoria).
/// Reagendar um agendamento varias vezes gera uma entrada por vez, preservando
/// o historico completo mesmo que Appointment.Slot so guarde o horario atual.
/// </summary>
public sealed class AppointmentChangeLogEntry : AggregateRoot<AppointmentChangeLogEntryId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public AppointmentId AppointmentId { get; private set; } = null!;

    public Guid CustomerId { get; private set; }

    public Guid ResourceId { get; private set; }

    public string ServiceName { get; private set; } = string.Empty;

    public AppointmentChangeType ChangeType { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset PreviousStartUtc { get; private set; }

    public DateTimeOffset? NewStartUtc { get; private set; }

    public bool ByStaff { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private AppointmentChangeLogEntry()
    {
    }

    private AppointmentChangeLogEntry(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, Guid resourceId, string serviceName, AppointmentChangeType changeType,
        string? reason, DateTimeOffset previousStartUtc, DateTimeOffset? newStartUtc, bool byStaff, DateTimeOffset occurredAtUtc)
        : base(AppointmentChangeLogEntryId.New())
    {
        TenantId = tenantId;
        AppointmentId = appointmentId;
        CustomerId = customerId;
        ResourceId = resourceId;
        ServiceName = serviceName;
        ChangeType = changeType;
        Reason = reason;
        PreviousStartUtc = previousStartUtc;
        NewStartUtc = newStartUtc;
        ByStaff = byStaff;
        OccurredAtUtc = occurredAtUtc;
    }

    public static Result<AppointmentChangeLogEntry> RecordCancellation(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, Guid resourceId, string serviceName,
        DateTimeOffset previousStartUtc, bool byStaff, string? reason, DateTimeOffset occurredAtUtc)
    {
        var trimmedReasonResult = TrimReason(reason);
        if (trimmedReasonResult.IsFailure)
        {
            return Result.Failure<AppointmentChangeLogEntry>(trimmedReasonResult.Error);
        }

        return Result.Success(new AppointmentChangeLogEntry(
            tenantId, appointmentId, customerId, resourceId, serviceName, AppointmentChangeType.Cancelled,
            trimmedReasonResult.Value, previousStartUtc, newStartUtc: null, byStaff, occurredAtUtc));
    }

    public static Result<AppointmentChangeLogEntry> RecordReschedule(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, Guid resourceId, string serviceName,
        DateTimeOffset previousStartUtc, DateTimeOffset newStartUtc, string? reason, DateTimeOffset occurredAtUtc)
    {
        var trimmedReasonResult = TrimReason(reason);
        if (trimmedReasonResult.IsFailure)
        {
            return Result.Failure<AppointmentChangeLogEntry>(trimmedReasonResult.Error);
        }

        return Result.Success(new AppointmentChangeLogEntry(
            tenantId, appointmentId, customerId, resourceId, serviceName, AppointmentChangeType.Rescheduled,
            trimmedReasonResult.Value, previousStartUtc, newStartUtc, byStaff: true, occurredAtUtc));
    }

    private static Result<string?> TrimReason(string? reason)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > 500 })
        {
            return Result.Failure<string?>(Error.Validation("AppointmentChangeLogEntry.ReasonTooLong", "O motivo pode ter no maximo 500 caracteres."));
        }

        return Result.Success(trimmed);
    }
}
