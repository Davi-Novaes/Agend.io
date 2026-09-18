using FluentValidation;

namespace Agendio.Modules.Identity.Application;

/// <summary>
/// Regra de senha forte compartilhada por todo fluxo que define/troca senha
/// (RegisterUser, ResetPassword, ChangePassword, AcceptInvitation) -- pedido
/// explicito do usuario: minimo 10 caracteres, 1 maiuscula, 1 simbolo. Nao
/// exige minuscula/numero de proposito (nao foi pedido, e adicionar sem
/// necessidade so atrapalha quem escolhe uma senha longa e forte que nao
/// bata com uma regra a mais).
/// </summary>
internal static class PasswordValidationRules
{
    public static IRuleBuilderOptions<T, string> RequireStrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("A senha precisa ter pelo menos 10 caracteres.")
            .Matches("[A-Z]")
            .WithMessage("A senha precisa ter pelo menos uma letra maiuscula.")
            .Matches("[^a-zA-Z0-9]")
            .WithMessage("A senha precisa ter pelo menos um simbolo (ex.: ! @ # $ %).");
}
