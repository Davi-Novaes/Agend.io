using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Um intervalo de funcionamento por dia da semana (ex.: Segunda 09:00-18:00).
/// O estabelecimento pode ter varios no mesmo dia (ex.: manha e tarde, com
/// intervalo de almoco entre eles). Mesmo molde de WorkingHoursEntry do modulo
/// Resources — nao compartilhado via SharedKernel de proposito, sao conceitos
/// de modulos diferentes que so coincidem em forma.
/// </summary>
public sealed class BusinessHoursEntry : ValueObject
{
    public DayOfWeek DayOfWeek { get; }

    public TimeOnly StartTime { get; }

    public TimeOnly EndTime { get; }

    private BusinessHoursEntry(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public static Result<BusinessHoursEntry> Create(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            return Result.Failure<BusinessHoursEntry>(
                Error.Validation("BusinessHoursEntry.InvalidRange", "O horario final precisa ser depois do horario inicial."));
        }

        return Result.Success(new BusinessHoursEntry(dayOfWeek, startTime, endTime));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DayOfWeek;
        yield return StartTime;
        yield return EndTime;
    }
}
