using FluentValidation;

namespace Agendio.Modules.Marketing.Application.SendCampaign;

public sealed class SendCampaignCommandValidator : AbstractValidator<SendCampaignCommand>
{
    public SendCampaignCommandValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(10000);
        RuleFor(c => c.Channel).IsInEnum();
        RuleFor(c => c.TargetSegment).IsInEnum().When(c => c.TargetSegment is not null);
    }
}
