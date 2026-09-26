using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.SetUnitBusinessHours;

public sealed class SetUnitBusinessHoursCommandValidator : AbstractValidator<SetUnitBusinessHoursCommand>
{
    public SetUnitBusinessHoursCommandValidator()
    {
        RuleForEach(command => command.Entries).ChildRules(entry =>
        {
            entry.RuleFor(value => value.EndTime)
                .GreaterThan(value => value.StartTime)
                .WithMessage("O horario final precisa ser depois do horario inicial.");
        });
    }
}
