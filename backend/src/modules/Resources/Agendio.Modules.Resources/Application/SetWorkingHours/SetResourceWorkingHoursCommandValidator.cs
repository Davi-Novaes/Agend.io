using FluentValidation;

namespace Agendio.Modules.Resources.Application.SetWorkingHours;

public sealed class SetResourceWorkingHoursCommandValidator : AbstractValidator<SetResourceWorkingHoursCommand>
{
    public SetResourceWorkingHoursCommandValidator()
    {
        RuleFor(c => c.ResourceId).NotEmpty();

        RuleForEach(c => c.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.EndTime).GreaterThan(e => e.StartTime)
                .WithMessage("O horario final precisa ser depois do horario inicial.");
        });
    }
}
