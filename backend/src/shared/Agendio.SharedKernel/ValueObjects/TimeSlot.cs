using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.ValueObjects;

/// <summary>
/// Intervalo de tempo em UTC. Usado pelo motor de agendamento (Sprint 3) para
/// representar a janela real ocupada por um Appointment (servico + buffers).
/// </summary>
public sealed class TimeSlot : ValueObject
{
    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    public TimeSpan Duration => EndUtc - StartUtc;

    private TimeSlot(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public static Result<TimeSlot> Create(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (endUtc <= startUtc)
        {
            return Result.Failure<TimeSlot>(Error.Validation("TimeSlot.InvalidRange", "O horario final precisa ser depois do horario inicial."));
        }

        return Result.Success(new TimeSlot(startUtc, endUtc));
    }

    public bool Overlaps(TimeSlot other) => StartUtc < other.EndUtc && other.StartUtc < EndUtc;

    public bool Contains(DateTimeOffset instant) => instant >= StartUtc && instant < EndUtc;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StartUtc;
        yield return EndUtc;
    }

    public override string ToString() => $"{StartUtc:u} - {EndUtc:u}";
}
