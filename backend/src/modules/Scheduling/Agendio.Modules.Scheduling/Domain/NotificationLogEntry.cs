using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>
/// Registro imutavel de uma tentativa de notificacao (e-mail ou WhatsApp) —
/// nunca editado ou apagado depois de criado (log de auditoria, mesmo
/// raciocinio de StockMovement no Estoque). "Historico de mensagens" da
/// Fase 7: um registro por tentativa, sucesso ou falha — uma falha seguida de
/// retry bem-sucedido do Hangfire aparece como duas linhas, o que e o
/// comportamento correto para auditoria (mostra que houve uma falha real).
/// </summary>
public sealed class NotificationLogEntry : AggregateRoot<NotificationLogEntryId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public AppointmentId AppointmentId { get; private set; } = null!;

    /// <summary>Referencia cru (nao tipado) — Customers e outro modulo, mesmo padrao de Appointment.CustomerId.</summary>
    public Guid CustomerId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public NotificationTrigger Trigger { get; private set; }

    public NotificationStatus Status { get; private set; }

    public DateTimeOffset SentAtUtc { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private NotificationLogEntry()
    {
    }

    private NotificationLogEntry(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, NotificationChannel channel, NotificationTrigger trigger,
        NotificationStatus status, DateTimeOffset sentAtUtc, string? errorMessage)
        : base(NotificationLogEntryId.New())
    {
        TenantId = tenantId;
        AppointmentId = appointmentId;
        CustomerId = customerId;
        Channel = channel;
        Trigger = trigger;
        Status = status;
        SentAtUtc = sentAtUtc;
        ErrorMessage = errorMessage;
    }

    public static Result<NotificationLogEntry> RecordSent(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, NotificationChannel channel, NotificationTrigger trigger,
        DateTimeOffset sentAtUtc) =>
        Result.Success(new NotificationLogEntry(
            tenantId, appointmentId, customerId, channel, trigger, NotificationStatus.Sent, sentAtUtc, errorMessage: null));

    public static Result<NotificationLogEntry> RecordFailed(
        TenantId tenantId, AppointmentId appointmentId, Guid customerId, NotificationChannel channel, NotificationTrigger trigger,
        DateTimeOffset attemptedAtUtc, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return Result.Failure<NotificationLogEntry>(
                Error.Validation("NotificationLogEntry.ErrorMessageRequired", "Uma falha registrada precisa de uma mensagem de erro."));
        }

        return Result.Success(new NotificationLogEntry(
            tenantId, appointmentId, customerId, channel, trigger, NotificationStatus.Failed, attemptedAtUtc, errorMessage.Trim()));
    }
}
