using FluentValidation;

namespace Agendio.Modules.Identity.Application.InviteTeamMember;

public sealed class InviteTeamMemberCommandValidator : AbstractValidator<InviteTeamMemberCommand>
{
    public InviteTeamMemberCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(320);
        RuleFor(c => c.Role).IsInEnum();
    }
}
