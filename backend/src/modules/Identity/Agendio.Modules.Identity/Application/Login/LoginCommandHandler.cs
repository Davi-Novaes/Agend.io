using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Infrastructure.Mfa;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.Login;

public sealed class LoginCommandHandler(
    IdentityDbContext dbContext,
    ITenantLookupService tenantLookupService,
    IPasswordHasher passwordHasher,
    IMfaChallengeStore mfaChallengeStore,
    IRefreshTokenGenerator refreshTokenGenerator,
    AuthTokenIssuer authTokenIssuer,
    IClock clock) : ICommandHandler<LoginCommand, LoginResult>
{
    private const int MfaChallengeLifetimeMinutes = 5;

    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenant = await tenantLookupService.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Result.Failure<LoginResult>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado ou inativo."));
        }

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<LoginResult>(Error.Unauthorized("Auth.InvalidCredentials", "E-mail ou senha invalidos."));
        }

        // ExplicitTenantBehavior ja ancorou o tenant no ITenantContext: o Global
        // Query Filter ja restringe esta busca ao tenant certo.
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == emailResult.Value, cancellationToken);

        // Mensagem identica para "nao existe" e "senha errada" — nao vazamos
        // se um e-mail esta cadastrado (evita enumeracao de contas).
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<LoginResult>(Error.Unauthorized("Auth.InvalidCredentials", "E-mail ou senha invalidos."));
        }

        // Codigo de erro DISTINTO do generico acima — diferente da checagem de
        // senha (onde esconder o motivo evita enumeracao de conta), aqui a pessoa
        // ja provou que e dona da conta ao acertar a senha. Esconder o motivo nao
        // ganha seguranca nenhuma e so deixa o usuario travado sem saber o que
        // fazer; o frontend usa este codigo pra mostrar um CTA de reenvio.
        if (user.EmailConfirmedAt is null)
        {
            return Result.Failure<LoginResult>(
                Error.Unauthorized("Auth.EmailNotConfirmed", "Confirme seu e-mail antes de entrar. Verifique sua caixa de entrada."));
        }

        if (user.MfaEnabled)
        {
            // Senha confirmou a identidade, mas o token final so sai depois do
            // segundo fator — ver VerifyMfaCommandHandler. Nenhum token (nem o
            // refresh cookie) e emitido neste ponto.
            var challengeToken = refreshTokenGenerator.GenerateToken();
            var challengeExpiresAtUtc = clock.UtcNow.AddMinutes(MfaChallengeLifetimeMinutes);

            await mfaChallengeStore.CreateAsync(challengeToken, user.Id, tenantId, challengeExpiresAtUtc, cancellationToken);

            return Result.Success<LoginResult>(new LoginMfaChallenge(challengeToken, challengeExpiresAtUtc));
        }

        var tokens = await authTokenIssuer.IssueAsync(user, tenant, cancellationToken);
        return Result.Success<LoginResult>(new LoginSuccess(tokens));
    }
}
