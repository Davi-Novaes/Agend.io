using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IdentityDbContext dbContext,
    ITenantLookupService tenantLookupService,
    IPasswordHasher passwordHasher) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenant = await tenantLookupService.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado ou inativo."));
        }

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<Guid>(emailResult.Error);
        }

        // ExplicitTenantBehavior ja ancorou o tenant no ITenantContext antes deste
        // handler rodar — o Global Query Filter do EF ja restringe isto ao tenant certo.
        var emailTaken = await dbContext.Users.AnyAsync(u => u.Email == emailResult.Value, cancellationToken);
        if (emailTaken)
        {
            return Result.Failure<Guid>(Error.Conflict("User.EmailTaken", "Ja existe uma conta com este e-mail neste estabelecimento."));
        }

        var passwordHash = passwordHasher.Hash(request.Password);

        var userResult = User.Register(tenantId, emailResult.Value, request.FullName, passwordHash);
        if (userResult.IsFailure)
        {
            return Result.Failure<Guid>(userResult.Error);
        }

        dbContext.Users.Add(userResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(userResult.Value.Id.Value);
    }
}
