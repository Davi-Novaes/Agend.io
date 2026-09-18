using Agendio.SharedKernel.ValueObjects;
using FluentValidation;

namespace Agendio.Modules.Billing.Application.SubscribeToPlan;

public sealed class SubscribeToPlanCommandValidator : AbstractValidator<SubscribeToPlanCommand>
{
    public SubscribeToPlanCommandValidator()
    {
        RuleFor(c => c.PlanId).NotEmpty();
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);

        // Mesmo padrao de PublicScheduleAppointmentCommandValidator: valida so
        // o FORMATO (digito verificador modulo 11) via o value object
        // compartilhado — antes disto, qualquer string de ate 20 caracteres
        // passava direto para a Asaas sem checagem nenhuma (P1-2, docs/AUTH_BILLING_SECURITY_AUDIT.md).
        RuleFor(c => c.CpfCnpj)
            .NotEmpty()
            .Must(cpfCnpj => CpfCnpj.Create(cpfCnpj).IsSuccess)
            .WithMessage("CPF/CNPJ invalido.");

        RuleFor(c => c.Email).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
    }
}
