using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Uma data especifica em que o estabelecimento nao atende (feriado, evento,
/// fechamento pontual) — sobrepoe o horario de funcionamento semanal so
/// naquele dia. Gerenciado manualmente pelo dono (o proprio sistema nao
/// assume nenhum calendario de feriados fixo, ja que o produto atende
/// negocios de segmentos e localidades diferentes).
/// </summary>
public sealed class ClosedDate : ValueObject
{
    public DateOnly Date { get; }

    public string? Reason { get; }

    private ClosedDate(DateOnly date, string? reason)
    {
        Date = date;
        Reason = reason;
    }

    public static ClosedDate Create(DateOnly date, string? reason) =>
        new(date, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Date;
        yield return Reason;
    }
}
