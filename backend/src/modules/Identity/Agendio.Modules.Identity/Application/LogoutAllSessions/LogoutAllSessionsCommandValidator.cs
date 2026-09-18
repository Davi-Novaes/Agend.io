using FluentValidation;

namespace Agendio.Modules.Identity.Application.LogoutAllSessions;

public sealed class LogoutAllSessionsCommandValidator : AbstractValidator<LogoutAllSessionsCommand>
{
    public LogoutAllSessionsCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
