using System.Security.Cryptography;
using System.Text;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed class VerifyCustomerPortalCodeCommandHandler(ICustomerPortalAccessStore accessStore)
    : ICommandHandler<VerifyCustomerPortalCodeCommand, CustomerPortalSession>
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

    public async Task<Result<CustomerPortalSession>> Handle(
        VerifyCustomerPortalCodeCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure || request.Code.Length != 6 || !request.Code.All(char.IsAsciiDigit))
        {
            return InvalidCode();
        }

        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Code)));
        var customerId = await accessStore.ConsumeChallengeAsync(
            request.TenantId, emailResult.Value.Value, codeHash);

        if (customerId is null)
        {
            return InvalidCode();
        }

        return await accessStore.CreateSessionAsync(request.TenantId, customerId.Value, SessionLifetime);
    }

    private static Result<CustomerPortalSession> InvalidCode() => Result.Failure<CustomerPortalSession>(
        Error.Unauthorized("CustomerPortal.InvalidCode", "O código é inválido ou expirou. Solicite um novo código."));
}
