using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Resources.Domain;

/// <summary>
/// Folga de um recurso (pessoa) — periodo em que ele nao esta disponivel para
/// agendamento. Registro imutavel apos criado: mudar de ideia significa
/// remover e criar de novo, nao editar (mesmo espirito de StockMovement no
/// modulo Estoque). Ainda NAO influencia o calculo de disponibilidade da
/// agenda — isso e responsabilidade da Fase 4 (motor de disponibilidade).
/// </summary>
public sealed class TimeOff : AggregateRoot<TimeOffId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public ResourceId ResourceId { get; private set; } = null!;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private TimeOff()
    {
    }

    private TimeOff(TenantId tenantId, ResourceId resourceId, DateOnly startDate, DateOnly endDate, string? reason)
        : base(TimeOffId.New())
    {
        TenantId = tenantId;
        ResourceId = resourceId;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
    }

    public static Result<TimeOff> Create(TenantId tenantId, ResourceId resourceId, DateOnly startDate, DateOnly endDate, string? reason)
    {
        if (endDate < startDate)
        {
            return Result.Failure<TimeOff>(
                Error.Validation("TimeOff.InvalidRange", "A data final precisa ser igual ou depois da data inicial."));
        }

        return Result.Success(new TimeOff(tenantId, resourceId, startDate, endDate, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
    }
}
