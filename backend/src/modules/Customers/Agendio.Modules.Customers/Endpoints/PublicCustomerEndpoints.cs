using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Customers.Application.PublicGetLoyaltyStatus;
using Agendio.Modules.Customers.Application.CustomerPortal;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Customers.Endpoints;

/// <summary>Superficie anonima consumida pelo portal publico do cliente — ver PublicCatalogEndpoints.</summary>
public sealed class PublicCustomerEndpoints : IEndpointModule
{
    private const string PortalSessionCookieName = "agendio_customer_session";

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var loyalty = endpoints.MapGroup("/api/public/tenants/{tenantId:guid}/loyalty").WithTags("Public Loyalty");

        loyalty.MapGet("/", async (Guid tenantId, string email, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new PublicGetLoyaltyStatusQuery(tenantId, email), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("PublicGetLoyaltyStatus")
        .WithSummary("Consulta o saldo de pontos de fidelidade de um cliente pelo e-mail, sem exigir login (Fase 11).");

        var portal = endpoints.MapGroup("/api/public/tenants/{tenantId:guid}/customer-portal")
            .WithTags("Customer Portal");

        portal.MapPost("/register", async (
            Guid tenantId, RegisterAccountRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new RegisterCustomerPortalAccountCommand(tenantId, request.FullName, request.Email, request.Phone);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireRateLimiting("auth")
        .WithName("RegisterCustomerPortalAccount")
        .WithSummary("Cliente cria a propria conta (nome, e-mail, telefone) sem precisar de agendamento previo nem do dono cadastrar. Envia codigo de acesso ao final, como no login.");

        portal.MapPost("/request-code", async (
            Guid tenantId, RequestAccessCodeRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.Send(new RequestCustomerPortalCodeCommand(tenantId, request.Email), cancellationToken);
            return Results.NoContent();
        })
        .RequireRateLimiting("auth")
        .WithName("RequestCustomerPortalCode")
        .WithSummary("Envia um codigo de acesso sem senha quando o e-mail pertence a um cliente ativo. Sempre responde 204.");

        portal.MapPost("/verify-code", async (
            Guid tenantId, VerifyAccessCodeRequest request, HttpContext httpContext,
            IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(
                new VerifyCustomerPortalCodeCommand(tenantId, request.Email, request.Code), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblemResult();
            }

            SetPortalSessionCookie(httpContext, tenantId, result.Value);
            return Results.Ok(new { result.Value.ExpiresAtUtc });
        })
        .RequireRateLimiting("auth")
        .WithName("VerifyCustomerPortalCode")
        .WithSummary("Confirma o codigo de uso unico e cria uma sessao HttpOnly exclusiva do cliente e do tenant.");

        portal.MapGet("/me", async (
            Guid tenantId, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var sessionToken = httpContext.Request.Cookies[PortalSessionCookieName];
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return Results.Unauthorized();
            }

            var result = await dispatcher.Query(new GetCustomerPortalQuery(tenantId, sessionToken), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetCustomerPortal")
        .WithSummary("Retorna perfil, fidelidade e agendamentos do cliente autenticado neste tenant.");

        portal.MapPost("/logout", async (
            Guid tenantId, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var sessionToken = httpContext.Request.Cookies[PortalSessionCookieName];
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                await dispatcher.Send(new LogoutCustomerPortalCommand(tenantId, sessionToken), cancellationToken);
            }

            DeletePortalSessionCookie(httpContext, tenantId);
            return Results.NoContent();
        })
        .WithName("LogoutCustomerPortal")
        .WithSummary("Revoga a sessao atual do portal do cliente e remove o cookie.");
    }

    private static void SetPortalSessionCookie(HttpContext httpContext, Guid tenantId, CustomerPortalSession session) =>
        httpContext.Response.Cookies.Append(PortalSessionCookieName, session.RawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = session.ExpiresAtUtc,
            Path = PortalCookiePath(tenantId),
            IsEssential = true,
        });

    private static void DeletePortalSessionCookie(HttpContext httpContext, Guid tenantId) =>
        httpContext.Response.Cookies.Delete(PortalSessionCookieName, new CookieOptions { Path = PortalCookiePath(tenantId) });

    private static string PortalCookiePath(Guid tenantId) => $"/api/public/tenants/{tenantId}/customer-portal";

    private sealed record RegisterAccountRequest(string FullName, string Email, string? Phone);

    private sealed record RequestAccessCodeRequest(string Email);

    private sealed record VerifyAccessCodeRequest(string Email, string Code);
}
