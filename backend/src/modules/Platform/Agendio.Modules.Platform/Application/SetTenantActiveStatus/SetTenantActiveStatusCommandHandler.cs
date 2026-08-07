using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Platform.Application.SetTenantActiveStatus;

public sealed class SetTenantActiveStatusCommandHandler(ITenantAdministrationService tenantAdministrationService)
    : ICommandHandler<SetTenantActiveStatusCommand>
{
    public Task<Result> Handle(SetTenantActiveStatusCommand request, CancellationToken cancellationToken) =>
        tenantAdministrationService.SetActiveStatusAsync(TenantId.From(request.TenantId), request.IsActive, cancellationToken);
}
