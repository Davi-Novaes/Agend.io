using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Resources.Domain;

/// <summary>
/// Um intervalo de disponibilidade recorrente por dia da semana (ex.: Segunda
/// 09:00-18:00). Um recurso pode ter varios no mesmo dia (ex.: manha e tarde,
/// com intervalo de almoco entre eles).
/// </summary>
public sealed class WorkingHoursEntry : ValueObject
{
    public DayOfWeek DayOfWeek { get; }

    public TimeOnly StartTime { get; }

    public TimeOnly EndTime { get; }

    private WorkingHoursEntry(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public static Result<WorkingHoursEntry> Create(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            return Result.Failure<WorkingHoursEntry>(
                Error.Validation("WorkingHoursEntry.InvalidRange", "O horario final precisa ser depois do horario inicial."));
        }

        return Result.Success(new WorkingHoursEntry(dayOfWeek, startTime, endTime));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DayOfWeek;
        yield return StartTime;
        yield return EndTime;
    }
}
