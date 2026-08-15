using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>
/// Fase 13 — cliente entra na fila quando nao ha horario disponivel para o
/// servico/data desejados. ResourceId e OPCIONAL (null = qualquer profissional
/// serve) — mesma logica de "sem preferencia" que Resource.UnitId usa em outro
/// contexto. Quando um agendamento compativel e cancelado, as entradas Waiting
/// sao notificadas (ver CancelAppointmentCommandHandler); a equipe confirma
/// manualmente convertendo UMA delas em agendamento — nao ha reserva automatica,
/// entao mais de uma entrada pode ser notificada para a mesma vaga.
/// </summary>
public sealed class WaitlistEntry : AggregateRoot<WaitlistEntryId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public Guid CustomerId { get; private set; }

    public Guid? ResourceId { get; private set; }

    public Guid ServiceId { get; private set; }

    public string ServiceName { get; private set; } = string.Empty;

    public DateOnly PreferredDate { get; private set; }

    public string? Notes { get; private set; }

    public WaitlistStatus Status { get; private set; }

    public DateTimeOffset? NotifiedAtUtc { get; private set; }

    public AppointmentId? ConvertedAppointmentId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private WaitlistEntry()
    {
    }

    private WaitlistEntry(
        TenantId tenantId, Guid customerId, Guid? resourceId, Guid serviceId, string serviceName, DateOnly preferredDate, string? notes)
        : base(WaitlistEntryId.New())
    {
        TenantId = tenantId;
        CustomerId = customerId;
        ResourceId = resourceId;
        ServiceId = serviceId;
        ServiceName = serviceName;
        PreferredDate = preferredDate;
        Notes = notes;
        Status = WaitlistStatus.Waiting;
    }

    public static Result<WaitlistEntry> Create(
        TenantId tenantId, Guid customerId, Guid? resourceId, Guid serviceId, string serviceName, DateOnly preferredDate, string? notes)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Result.Failure<WaitlistEntry>(Error.Validation("WaitlistEntry.ServiceNameRequired", "Nome do servico e obrigatorio."));
        }

        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmedNotes is { Length: > 500 })
        {
            return Result.Failure<WaitlistEntry>(Error.Validation("WaitlistEntry.NotesTooLong", "As observacoes podem ter no maximo 500 caracteres."));
        }

        return Result.Success(new WaitlistEntry(tenantId, customerId, resourceId, serviceId, serviceName.Trim(), preferredDate, trimmedNotes));
    }

    public Result MarkNotified(DateTimeOffset nowUtc)
    {
        if (Status != WaitlistStatus.Waiting)
        {
            return Result.Failure(Error.Validation("WaitlistEntry.InvalidTransition", "So e possivel notificar uma entrada Aguardando."));
        }

        Status = WaitlistStatus.Notified;
        NotifiedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Convert(AppointmentId appointmentId)
    {
        if (Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
        {
            return Result.Failure(Error.Validation("WaitlistEntry.InvalidTransition", "So e possivel confirmar uma entrada Aguardando ou Notificada."));
        }

        Status = WaitlistStatus.Converted;
        ConvertedAppointmentId = appointmentId;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status is WaitlistStatus.Converted or WaitlistStatus.Cancelled)
        {
            return Result.Failure(Error.Validation("WaitlistEntry.InvalidTransition", "Esta entrada nao pode mais ser cancelada."));
        }

        Status = WaitlistStatus.Cancelled;
        return Result.Success();
    }
}
