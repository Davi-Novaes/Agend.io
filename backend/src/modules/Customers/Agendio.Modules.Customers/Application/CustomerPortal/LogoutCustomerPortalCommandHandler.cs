using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed class LogoutCustomerPortalCommandHandler(ICustomerPortalAccessStore accessStore)
    : ICommandHandler<LogoutCustomerPortalCommand>
{
    public async Task<Result> Handle(LogoutCustomerPortalCommand request, CancellationToken cancellationToken)
    {
        await accessStore.RevokeSessionAsync(request.TenantId, request.SessionToken);
        return Result.Success();
    }
}
