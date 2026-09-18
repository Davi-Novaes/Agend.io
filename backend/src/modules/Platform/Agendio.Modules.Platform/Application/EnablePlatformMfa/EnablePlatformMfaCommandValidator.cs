using FluentValidation;

namespace Agendio.Modules.Platform.Application.EnablePlatformMfa;

public sealed class EnablePlatformMfaCommandValidator : AbstractValidator<EnablePlatformMfaCommand>
{
    public EnablePlatformMfaCommandValidator()
    {
        RuleFor(c => c.AdminId).NotEmpty();
        RuleFor(c => c.Secret).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().Length(6);
    }
}
