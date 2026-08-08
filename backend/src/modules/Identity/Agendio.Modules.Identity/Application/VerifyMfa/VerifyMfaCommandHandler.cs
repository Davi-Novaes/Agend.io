using Agendio.Modules.Identity.Infrastructure.Mfa;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.VerifyMfa;

public sealed class VerifyMfaCommandHandler(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    ITenantLookupService tenantLookupService,
    IMfaChallengeStore mfaChallengeStore,
    IMfaCodeVerifier mfaCodeVerifier,
    AuthTokenIssuer authTokenIssuer) : ICommandHandler<VerifyMfaCommand, AuthTokensResult>
{
    private static readonly Error InvalidChallengeError =
        Error.Unauthorized("Auth.InvalidMfaChallenge", "Sessao de verificacao invalida ou expirada. Faca login novamente.");

    private static readonly Error InvalidCodeError = Error.Unauthorized("Auth.InvalidMfaCode", "Codigo invalido.");

    public async Task<Result<AuthTokensResult>> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        // Uso unico: se ConsumeAsync ja apagou este token numa chamada anterior
        // (sucesso ou nao), reapresenta-lo aqui sempre falha.
        var challenge = await mfaChallengeStore.ConsumeAsync(request.ChallengeToken, cancellationToken);
        if (challenge is null)
        {
            return Result.Failure<AuthTokensResult>(InvalidChallengeError);
        }

        // A partir daqui o tenant e conhecido — mesmo padrao de
        // RefreshAccessTokenCommandHandler (ADR-0002): ancora antes de qualquer
        // outra leitura para que o Global Query Filter e a RLS operem certo.
        tenantContext.SetTenant(challenge.TenantId);

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken);
        if (user is null || !user.IsActive || !user.MfaEnabled)
        {
            return Result.Failure<AuthTokensResult>(InvalidChallengeError);
        }

        if (!await mfaCodeVerifier.VerifyAsync(user, request.Code, cancellationToken))
        {
            return Result.Failure<AuthTokensResult>(InvalidCodeError);
        }

        var tenant = await tenantLookupService.FindByIdAsync(challenge.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Result.Failure<AuthTokensResult>(InvalidChallengeError);
        }

        var tokens = await authTokenIssuer.IssueAsync(user, tenant, cancellationToken);
        return Result.Success(tokens);
    }
}
