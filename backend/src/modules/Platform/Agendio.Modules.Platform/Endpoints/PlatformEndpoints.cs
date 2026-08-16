using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Application.CancelSubscriptionForTenant;
using Agendio.Modules.Platform.Application.GetPlatformDashboardMetrics;
using Agendio.Modules.Platform.Application.ListSubscriptionsForPlatform;
using Agendio.Modules.Platform.Application.ListTenants;
using Agendio.Modules.Platform.Application.LoginPlatformAdmin;
using Agendio.Modules.Platform.Application.SetTenantActiveStatus;
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
            var result = await dispatcher.Send(new LoginPlatformAdminCommand(request.Email, request.Password), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("LoginPlatformAdmin")
        .WithSummary("Login do Super Admin da plataforma — autoridade separada de qualquer tenant.");

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
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record SetActiveStatusRequest(bool IsActive);
}
