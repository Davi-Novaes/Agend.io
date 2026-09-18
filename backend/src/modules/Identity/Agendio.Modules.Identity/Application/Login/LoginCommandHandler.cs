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
    SecurityAuditLogger<IdentityDbContext> securityAuditLogger,
    IClock clock) : ICommandHandler<LoginCommand, LoginResult>
{
    private const int MfaChallengeLifetimeMinutes = 5;
    private static readonly Error InvalidCredentialsError = Error.Unauthorized("Auth.InvalidCredentials", "E-mail ou senha invalidos.");

    /// <summary>
    /// Codigo distinto do generico acima — a pessoa ja disparou o bloqueio (5
    /// tentativas erradas) e mostrar quanto tempo falta e um pedido de produto
    /// explicito (evita o usuario ficar martelando "esqueci minha senha" sem
    /// saber que so precisa esperar). Aceita o pequeno risco de enumeracao
    /// (revelar que ESTA conta existe e esta bloqueada) como o EmailNotConfirmed
    /// ja faz — so alcancavel apos 5 tentativas na MESMA conta, nao de graca.
    /// </summary>
    private static Error AccountLockedError(DateTimeOffset lockedUntilUtc) =>
        Error.Unauthorized(
            "Auth.AccountLocked",
            "Muitas tentativas incorretas. Tente novamente mais tarde ou redefina sua senha.",
            new Dictionary<string, object?> { ["lockedUntilUtc"] = lockedUntilUtc });

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
            await LogAsync("LoginFailed", success: false, tenantId, actorId: null, "invalid-email-format", cancellationToken);
            return Result.Failure<LoginResult>(InvalidCredentialsError);
        }

        // ExplicitTenantBehavior ja ancorou o tenant no ITenantContext: o Global
        // Query Filter ja restringe esta busca ao tenant certo.
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == emailResult.Value, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await LogAsync("LoginFailed", success: false, tenantId, actorId: null, "user-not-found-or-inactive", cancellationToken);
            return Result.Failure<LoginResult>(InvalidCredentialsError);
        }

        // Conta trancada: pula a verificacao de senha (evita o custo de Argon2 e
        // um pequeno sinal de timing) e NAO soma mais tentativas — a conta ja
        // esta contada. Mostra quanto tempo falta (ver AccountLockedError).
        if (user.IsLockedOut(clock.UtcNow))
        {
            await LogAsync("LoginFailed", success: false, tenantId, user.Id.Value, "account-locked", cancellationToken);
            return Result.Failure<LoginResult>(AccountLockedError(user.LockedUntilUtc!.Value));
        }

        // Mensagem identica para "nao existe" e "senha errada" — nao vazamos
        // se um e-mail esta cadastrado (evita enumeracao de contas).
        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLoginAttempt(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            var lockedNow = user.IsLockedOut(clock.UtcNow);
            await LogAsync(lockedNow ? "AccountLocked" : "LoginFailed", success: false, tenantId, user.Id.Value, "wrong-password", cancellationToken);

            // Esta tentativa foi a que estourou o limite: mostra o aviso de
            // bloqueio ja aqui, em vez de deixar a pessoa tentar de novo (com a
            // senha certa, ate) so pra descobrir que esta trancada.
            return Result.Failure<LoginResult>(lockedNow ? AccountLockedError(user.LockedUntilUtc!.Value) : InvalidCredentialsError);
        }

        // Senha correta: a pessoa provou posse da conta, mesmo que o login nao
        // conclua por outro motivo abaixo (e-mail nao confirmado, MFA pendente).
        // Zera o contador aqui, nao so no fim do metodo.
        user.RegisterSuccessfulLogin();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Codigo de erro DISTINTO do generico acima — diferente da checagem de
        // senha (onde esconder o motivo evita enumeracao de conta), aqui a pessoa
        // ja provou que e dona da conta ao acertar a senha. Esconder o motivo nao
        // ganha seguranca nenhuma e so deixa o usuario travado sem saber o que
        // fazer; o frontend usa este codigo pra mostrar um CTA de reenvio.
        if (user.EmailConfirmedAt is null)
        {
            await LogAsync("LoginBlocked", success: false, tenantId, user.Id.Value, "email-not-confirmed", cancellationToken);
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
            await LogAsync("LoginMfaChallengeIssued", success: true, tenantId, user.Id.Value, null, cancellationToken);

            return Result.Success<LoginResult>(new LoginMfaChallenge(challengeToken, challengeExpiresAtUtc));
        }

        var tokens = await authTokenIssuer.IssueAsync(user, tenant, cancellationToken);
        await LogAsync("LoginSucceeded", success: true, tenantId, user.Id.Value, null, cancellationToken);

        return Result.Success<LoginResult>(new LoginSuccess(tokens));
    }

    private Task LogAsync(string eventType, bool success, TenantId tenantId, Guid? actorId, string? reason, CancellationToken cancellationToken) =>
        securityAuditLogger.LogAsync(eventType, success, tenantId.Value, actorId, reason is null ? null : $"{{\"reason\":\"{reason}\"}}", cancellationToken);
}
