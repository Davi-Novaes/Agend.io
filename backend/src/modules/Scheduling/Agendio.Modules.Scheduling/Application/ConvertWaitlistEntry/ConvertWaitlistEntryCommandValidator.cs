using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.ConvertWaitlistEntry;

public sealed class ConvertWaitlistEntryCommandValidator : AbstractValidator<ConvertWaitlistEntryCommand>
{
    public ConvertWaitlistEntryCommandValidator()
    {
        RuleFor(c => c.WaitlistEntryId).NotEmpty();
        RuleFor(c => c.ResourceId).NotEmpty();
        RuleFor(c => c.StartAtUtc).NotEmpty();
    }
}
