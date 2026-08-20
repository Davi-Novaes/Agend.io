using System.Security.Claims;
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
    IBackgroundJobClient jobClient,
    IOnboardingJwtTokenService onboardingJwtTokenService) : ICommandHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<Result<RegisterUserResult>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenant = await tenantLookupService.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Result.Failure<RegisterUserResult>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado ou inativo."));
        }

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<RegisterUserResult>(emailResult.Error);
        }

        // ExplicitTenantBehavior ja ancorou o tenant no ITenantContext antes deste
        // handler rodar — o Global Query Filter do EF ja restringe isto ao tenant certo.
        var emailTaken = await dbContext.Users.AnyAsync(u => u.Email == emailResult.Value, cancellationToken);
        if (emailTaken)
        {
            return Result.Failure<RegisterUserResult>(Error.Conflict("User.EmailTaken", "Ja existe uma conta com este e-mail neste estabelecimento."));
        }

        var passwordHash = passwordHasher.Hash(request.Password);

        var userResult = User.Register(tenantId, emailResult.Value, request.FullName, passwordHash);
        if (userResult.IsFailure)
        {
            return Result.Failure<RegisterUserResult>(userResult.Error);
        }

        dbContext.Users.Add(userResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Via Hangfire (nao sincrono): e o unico jeito do dono entrar na propria
        // conta (login exige e-mail confirmado, ver LoginCommandHandler), uma
        // falha de SMTP precisa de retry automatico. O job gera o token de
        // confirmacao internamente (ver EmailConfirmationJobs).
        var userId = userResult.Value.Id.Value;
        jobClient.Enqueue<EmailConfirmationJobs>(job => job.SendConfirmationEmailAsync(tenantId.Value, userId, CancellationToken.None));

        // Prova posse do tenant recem-criado pro restante do onboarding (escolha
        // de plano) — substitui o antigo "TenantId so afirmado no corpo" que
        // deixava o endpoint de billing anonimo ativar assinatura de qualquer
        // tenant (BL-01, docs/BACKLOG.md).
        var onboardingClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(OnboardingAuthConstants.TenantIdClaimType, tenantId.Value.ToString()),
        };
        var (onboardingToken, onboardingTokenExpiresAtUtc) = onboardingJwtTokenService.GenerateAccessToken(onboardingClaims);

        return Result.Success(new RegisterUserResult(userId, onboardingToken, onboardingTokenExpiresAtUtc));
    }
}
