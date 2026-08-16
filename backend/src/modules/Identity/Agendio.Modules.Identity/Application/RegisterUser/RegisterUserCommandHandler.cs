using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Notifications;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IdentityDbContext dbContext,
    ITenantLookupService tenantLookupService,
    IPasswordHasher passwordHasher,
    IBackgroundJobClient jobClient) : ICommandHandler<RegisterUserCommand, Guid>
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

        // Via Hangfire (nao sincrono): e o unico jeito do dono entrar na propria
        // conta (login exige e-mail confirmado, ver LoginCommandHandler), uma
        // falha de SMTP precisa de retry automatico. O job gera o token de
        // confirmacao internamente (ver EmailConfirmationJobs).
        var userId = userResult.Value.Id.Value;
        jobClient.Enqueue<EmailConfirmationJobs>(job => job.SendConfirmationEmailAsync(tenantId.Value, userId, CancellationToken.None));

        return Result.Success(userId);
    }
}
