using System.Security.Claims;
using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Application.CancelSubscriptionForTenant;
using Agendio.Modules.Platform.Application.DisablePlatformMfa;
using Agendio.Modules.Platform.Application.EnablePlatformMfa;
using Agendio.Modules.Platform.Application.GetFeedbackForPlatform;
using Agendio.Modules.Platform.Application.GetPlatformDashboardMetrics;
using Agendio.Modules.Platform.Application.GetPlatformMfaStatus;
using Agendio.Modules.Platform.Application.GetSecurityActivityLog;
using Agendio.Modules.Platform.Application.ListSubscriptionsForPlatform;
using Agendio.Modules.Platform.Application.ListTenants;
using Agendio.Modules.Platform.Application.LoginPlatformAdmin;
using Agendio.Modules.Platform.Application.SetTenantActiveStatus;
using Agendio.Modules.Platform.Application.SetupPlatformMfa;
using Agendio.Modules.Platform.Application.VerifyPlatformMfa;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Platform.Endpoints;

/// <summary>
/// Rotas do Super Admin. Tudo aqui, exceto o login, exige a policy PlatformOnly
/// (scheme "Platform" + claim scope=platform) — um token de tenant NUNCA
/// autentica aqui, mesmo que o mesmo bearer token seja usado por engano, porque
/// issuer/audience/chave sao de uma autoridade completamente diferente.
/// </summary>
public sealed class PlatformEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/platform").WithTags("Platform");

        group.MapPost("/auth/login", async (LoginRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new LoginPlatformAdminCommand(request.Email, request.Password, request.TurnstileToken), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemResult();
            }

            return result.Value switch
            {
                LoginPlatformAdminSuccess success => Results.Ok(ToResponse(success.Result)),
                LoginPlatformAdminMfaChallenge challenge => Results.Ok(new
                {
                    mfaRequired = true,
                    mfaChallengeToken = challenge.ChallengeToken,
                    expiresAtUtc = challenge.ExpiresAtUtc,
                }),
                _ => throw new InvalidOperationException($"LoginPlatformAdminOutcome inesperado: {result.Value.GetType().Name}."),
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("LoginPlatformAdmin")
        .WithSummary("Login do Super Admin da plataforma — autoridade separada de qualquer tenant.");

        group.MapPost("/auth/mfa/verify", async (VerifyMfaRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new VerifyPlatformMfaCommand(request.MfaChallengeToken, request.Code), cancellationToken);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("VerifyPlatformMfa")
        .WithSummary("Segunda etapa do login do Super Admin quando MFA esta habilitado.");

        group.MapGet("/auth/mfa/status", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetPlatformMfaStatusQuery(GetAdminId(httpContext)), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("GetPlatformMfaStatus")
        .WithSummary("Diz se o Super Admin autenticado tem MFA habilitado.");

        group.MapPost("/auth/mfa/setup", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetupPlatformMfaCommand(GetAdminId(httpContext)), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("SetupPlatformMfa")
        .WithSummary("Gera um novo segredo TOTP e a URI de provisionamento (QR code) para o Super Admin — nada e persistido ate EnablePlatformMfa confirmar.");

        group.MapPost("/auth/mfa/enable", async (EnableMfaRequest request, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new EnablePlatformMfaCommand(GetAdminId(httpContext), request.Secret, request.Code), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("EnablePlatformMfa")
        .WithSummary("Confirma o codigo TOTP e ativa MFA para o Super Admin.");

        group.MapPost("/auth/mfa/disable", async (DisableMfaRequest request, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new DisablePlatformMfaCommand(GetAdminId(httpContext), request.Password, request.Code), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("DisablePlatformMfa")
        .WithSummary("Desliga MFA do Super Admin — exige senha e codigo TOTP validos.");

        group.MapGet("/tenants", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new ListTenantsQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("ListTenantsForPlatform")
        .WithSummary("Lista todos os tenants da plataforma, inclusive inativos.");

        group.MapPatch("/tenants/{id:guid}/status", async (Guid id, SetActiveStatusRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetTenantActiveStatusCommand(id, request.IsActive), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("SetTenantActiveStatusForPlatform")
        .WithSummary("Ativa ou desativa um tenant — desativado bloqueia login de todos os usuarios daquele estabelecimento.");

        group.MapGet("/subscriptions", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new ListSubscriptionsForPlatformQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("ListSubscriptionsForPlatform")
        .WithSummary("Lista a assinatura de todos os tenants (trial, ativa, atrasada, cancelada).");

        group.MapPost("/subscriptions/{tenantId:guid}/cancel", async (Guid tenantId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new CancelSubscriptionForTenantCommand(tenantId), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("CancelSubscriptionForTenant")
        .WithSummary("Cancela a assinatura de um tenant a pedido do Super Admin — chama a Asaas de verdade, acao irreversivel.");

        group.MapGet("/dashboard", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetPlatformDashboardMetricsQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("GetPlatformDashboardMetrics")
        .WithSummary("Metricas agregadas do SaaS: total/novos estabelecimentos, distribuicao de assinaturas por status e MRR.");

        group.MapGet("/security/activity", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetSecurityActivityLogQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("GetSecurityActivityLog")
        .WithSummary("Ultimos 14 dias de eventos de login/cadastro (sucesso e falha) de todos os tenants e do proprio Super Admin, com IP e user agent.");

        group.MapGet("/feedback", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetFeedbackForPlatformQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(PlatformAuthConstants.AuthorizationPolicy)
        .WithName("GetFeedbackForPlatform")
        .WithSummary("Feedback livre enviado por usuarios de qualquer tenant, mais recente primeiro.");
    }

    // mfaRequired:false aqui deixa o frontend tratar a resposta de /auth/login e
    // de /auth/mfa/verify com o MESMO formato (mesmo padrao de IdentityEndpoints.ToResponse).
    private static object ToResponse(LoginPlatformAdminResult result) => new
    {
        mfaRequired = false,
        accessToken = result.AccessToken,
        expiresAtUtc = result.ExpiresAtUtc,
        fullName = result.FullName,
    };

    // AdminId sempre presente nas rotas com a policy PlatformOnly — a claim vem
    // do JWT emitido em LoginPlatformAdminCommandHandler/VerifyPlatformMfaCommandHandler.
    private static Guid GetAdminId(HttpContext httpContext) =>
        Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private sealed record LoginRequest(string Email, string Password, string TurnstileToken);

    private sealed record VerifyMfaRequest(string MfaChallengeToken, string Code);

    private sealed record EnableMfaRequest(string Secret, string Code);

    private sealed record DisableMfaRequest(string Password, string Code);

    private sealed record SetActiveStatusRequest(bool IsActive);
}
