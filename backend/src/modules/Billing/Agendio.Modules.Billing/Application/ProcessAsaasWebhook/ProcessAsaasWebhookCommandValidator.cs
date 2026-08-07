using FluentValidation;

namespace Agendio.Modules.Billing.Application.ProcessAsaasWebhook;

public sealed class ProcessAsaasWebhookCommandValidator : AbstractValidator<ProcessAsaasWebhookCommand>
{
    public ProcessAsaasWebhookCommandValidator()
    {
        RuleFor(c => c.Event).NotEmpty();
        RuleFor(c => c.AsaasPaymentId).NotEmpty();
    }
}
