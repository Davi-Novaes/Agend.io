using Agendio.Modules.Identity.Application;
using Agendio.SharedKernel.ValueObjects;
using FluentValidation;

namespace Agendio.Modules.Identity.Application.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().MaximumLength(320);
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);

        RuleFor(c => c.Password).RequireStrongPassword();

        // Telefone/CPF-CNPJ do dono/responsavel: obrigatorios so aqui (nao no
        // dominio, ver User.Register) porque um membro de equipe convidado
        // nao precisa deles — mesmo padrao de CpfCnpj em
        // PublicScheduleAppointmentCommandValidator, so que sem o "ou vazio"
        // (aqui e sempre obrigatorio).
        RuleFor(c => c.Phone)
            .NotEmpty()
            .Must(phone => PhoneNumber.Create(phone).IsSuccess)
            .WithMessage("Telefone invalido.");

        RuleFor(c => c.CpfCnpj)
            .NotEmpty()
            .Must(cpfCnpj => CpfCnpj.Create(cpfCnpj).IsSuccess)
            .WithMessage("CPF/CNPJ invalido.");

        RuleFor(c => c.TermsAccepted)
            .Equal(true)
            .WithMessage("E preciso aceitar os Termos de Uso e a Politica de Privacidade.");

        // Sem NotEmpty aqui de proposito — ver comentario em LoginCommand
        // sobre TurnstileToken: a obrigatoriedade e condicional a
        // Turnstile:SecretKey estar configurado, verificado no pipeline, nao aqui.
    }
}
