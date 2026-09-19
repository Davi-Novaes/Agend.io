using Agendio.Modules.Customers.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

/// <summary>
/// Cliente cria a propria conta sem depender do dono cadastrar antes nem de
/// um agendamento previo — mesmo find-or-create de e-mail do agendamento
/// publico (ICustomerRegistrationService), so que disparado direto da tela
/// "Criar conta" do portal. Se o e-mail ja pertencer a um cliente, so envia
/// o codigo de acesso (nao reescreve nome/telefone de um cadastro existente).
/// </summary>
public sealed class RegisterCustomerPortalAccountCommandHandler(
    ICustomerRegistrationService customerRegistration,
    CustomerPortalAccessCodeSender accessCodeSender) : ICommandHandler<RegisterCustomerPortalAccountCommand>
{
    public async Task<Result> Handle(RegisterCustomerPortalAccountCommand request, CancellationToken cancellationToken)
    {
        var registerResult = await customerRegistration.FindOrRegisterByEmailAsync(
            request.FullName, request.Email, request.Phone, cancellationToken: cancellationToken);

        if (registerResult.IsFailure)
        {
            return Result.Failure(registerResult.Error);
        }

        var normalizedEmail = Email.Create(request.Email).Value.Value;
        await accessCodeSender.SendIfQuotaAvailableAsync(request.TenantId, registerResult.Value, normalizedEmail, cancellationToken);

        return Result.Success();
    }
}
