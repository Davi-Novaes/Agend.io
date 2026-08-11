using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>
/// O agendamento em si — a raiz do motor de agenda. CustomerId/ResourceId/ServiceId
/// sao Guid puro (nao os Ids fortemente tipados dos outros modulos): Scheduling
/// nunca referencia Customers/Catalog/Resources.Domain, so os respectivos
/// .Contracts (ver regra de dependencia em CLAUDE.md), que devolvem Guid.
///
/// ServiceName e Price sao SNAPSHOT do momento da criacao — mudar o preco do
/// servico depois nao reescreve agendamentos ja marcados.
///
/// Protecao contra overbooking NAO e feita aqui: e uma EXCLUDE constraint no
/// Postgres (ver migration) sobre (tenant_id, resource_id, time_range). Duas
/// requisicoes simultaneas no mesmo recurso/horario sempre resultam em exatamente
/// um INSERT bem-sucedido, mesmo sob concorrencia real — nenhum lock de aplicacao
/// garante isso de forma confiavel.
/// </summary>
public sealed class Appointment : AggregateRoot<AppointmentId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public Guid CustomerId { get; private set; }

    public Guid ResourceId { get; private set; }

    /// <summary>Snapshot do Resource.UnitId no momento do agendamento — mudar a unidade do recurso depois nao reescreve agendamentos ja marcados.</summary>
    public Guid? UnitId { get; private set; }

    public Guid ServiceId { get; private set; }

    public string ServiceName { get; private set; } = string.Empty;

    public TimeSlot Slot { get; private set; } = null!;

    public Money Price { get; private set; } = null!;

    public AppointmentStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private Appointment()
    {
    }

    private Appointment(
        TenantId tenantId, Guid customerId, Guid resourceId, Guid? unitId, Guid serviceId, string serviceName, TimeSlot slot, Money price,
        string? notes)
        : base(AppointmentId.New())
    {
        TenantId = tenantId;
        CustomerId = customerId;
        ResourceId = resourceId;
        UnitId = unitId;
        ServiceId = serviceId;
        ServiceName = serviceName;
        Slot = slot;
        Price = price;
        Notes = notes;
        Status = AppointmentStatus.Scheduled;
    }

    public static Result<Appointment> Schedule(
        TenantId tenantId, Guid customerId, Guid resourceId, Guid? unitId, Guid serviceId, string serviceName, TimeSlot slot, Money price,
        string? notes)
    {
        var appointment = new Appointment(tenantId, customerId, resourceId, unitId, serviceId, serviceName, slot, price, notes?.Trim());
        appointment.Raise(new AppointmentScheduledDomainEvent(appointment.Id, tenantId, resourceId, slot.StartUtc, slot.EndUtc));

        return Result.Success(appointment);
    }

    public Result Confirm()
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            return Result.Failure(Error.Validation("Appointment.InvalidTransition", "So e possivel confirmar um agendamento com status Agendado."));
        }

        Status = AppointmentStatus.Confirmed;
        return Result.Success();
    }

    public Result Start()
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            return Result.Failure(Error.Validation("Appointment.InvalidTransition", "So e possivel iniciar um agendamento Agendado ou Confirmado."));
        }

        Status = AppointmentStatus.InProgress;
        return Result.Success();
    }

    public Result Complete(DateTimeOffset nowUtc)
    {
        if (Status != AppointmentStatus.InProgress)
        {
            return Result.Failure(Error.Validation("Appointment.InvalidTransition", "So e possivel concluir um agendamento Em andamento."));
        }

        Status = AppointmentStatus.Completed;
        Raise(new AppointmentCompletedDomainEvent(Id, TenantId, ResourceId, ServiceName, Price, nowUtc));
        return Result.Success();
    }

    public Result MarkNoShow()
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            return Result.Failure(Error.Validation("Appointment.InvalidTransition", "So e possivel marcar Nao compareceu em um agendamento Agendado ou Confirmado."));
        }

        Status = AppointmentStatus.NoShow;
        return Result.Success();
    }

    public Result CancelByCustomer() => Cancel(AppointmentStatus.CancelledByCustomer);

    public Result CancelByStaff() => Cancel(AppointmentStatus.CancelledByStaff);

    private Result Cancel(AppointmentStatus cancelledStatus)
    {
        if (Status is AppointmentStatus.Completed or AppointmentStatus.CancelledByCustomer or AppointmentStatus.CancelledByStaff or AppointmentStatus.NoShow)
        {
            return Result.Failure(Error.Validation("Appointment.InvalidTransition", "Este agendamento nao pode mais ser cancelado."));
        }

        Status = cancelledStatus;
        return Result.Success();
    }

    public Result Reschedule(TimeSlot newSlot)
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            return Result.Failure(Error.Validation("Appointment.InvalidTransition", "So e possivel remarcar um agendamento Agendado ou Confirmado."));
        }

        Slot = newSlot;
        return Result.Success();
    }
}
