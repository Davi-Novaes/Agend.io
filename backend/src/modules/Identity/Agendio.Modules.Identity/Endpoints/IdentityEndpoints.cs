using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Identity.Application;
using Agendio.Modules.Identity.Application.AcceptInvitation;
using Agendio.Modules.Identity.Application.InviteTeamMember;
using Agendio.Modules.Identity.Application.Login;
using Agendio.Modules.Identity.Application.RefreshAccessToken;
using Agendio.Modules.Identity.Application.RegisterUser;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Endpoints;

public sealed class IdentityEndpoints : IEndpointModule
{
    // Path restrito a /api/auth: o navegador so envia este cookie para as rotas
    // que realmente precisam dele, reduzindo a superficie de CSRF.
    private const string RefreshTokenCookieName = "agendio_refresh_token";

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Identity");

        group.MapPost("/register", async (RegisterRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new RegisterUserCommand(request.TenantId, request.Email, request.Password, request.FullName);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/tenants/{request.TenantId}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .WithName("RegisterUser")
        .WithSummary("Registra o primeiro usuario (dono) de um estabelecimento.");

        group.MapPost("/login", async (LoginRequest request, IDispatcher dispatcher, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var command = new LoginCommand(request.TenantId, request.Email, request.Password);
            var result = await dispatcher.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.ToProblemResult();
            }

            SetRefreshTokenCookie(httpContext, result.Value);
            return Results.Ok(ToResponse(result.Value));
        })
        .AllowAnonymous()
        .WithName("Login");

        group.MapPost("/refresh", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var refreshToken = httpContext.Request.Cookies[RefreshTokenCookieName];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.Unauthorized();
            }

            var command = new RefreshAccessTokenCommand(refreshToken);
            var result = await dispatcher.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                // Cookie invalido/reutilizado: remove para o cliente nao ficar
                // reenviando um token que so vai continuar falhando.
                httpContext.Response.Cookies.Delete(RefreshTokenCookieName);
                return result.Error.ToProblemResult();
            }

            SetRefreshTokenCookie(httpContext, result.Value);
            return Results.Ok(ToResponse(result.Value));
        })
        .AllowAnonymous()
        .WithName("RefreshAccessToken");

        var team = endpoints.MapGroup("/api/team").WithTags("Team");

        team.MapPost("/invitations", async (InviteTeamMemberRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new InviteTeamMemberCommand(request.Email, request.Role);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/team/invitations/{result.Value.InvitationId}", result.Value)
                : result.Error.ToProblemResult();
        })
        // "Owner" e um literal por proposito (ver TenancyEndpoints.UpdateTenantBranding).
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("InviteTeamMember")
        .WithSummary("Convida um novo membro para a equipe do estabelecimento.");

        team.MapPost("/invitations/{token}/accept", async (string token, AcceptInvitationRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new AcceptInvitationCommand(token, request.FullName, request.Password);
            var result = await dispatcher.Send(command, cancellationToken);

            // Sem endpoint de "buscar usuario por id" ainda para apontar um
            // Location — 200 com o id criado e mais honesto que um 201 sintetico.
            return result.IsSuccess
                ? Results.Ok(new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        // Publica de proposito: quem aceita o convite ainda nao tem conta/token.
        .AllowAnonymous()
        .WithName("AcceptTeamInvitation")
        .WithSummary("Aceita um convite de equipe e cria a conta do novo membro.");

        team.MapGet("/members", async (IdentityDbContext dbContext, CancellationToken cancellationToken) =>
        {
            // O Global Query Filter ja restringe isto ao tenant do JWT do chamador.
            var members = await dbContext.Users
                .OrderBy(u => u.CreatedAtUtc)
                .Select(u => new { id = u.Id.Value, email = u.Email.Value, u.FullName, role = u.Role.ToString(), u.IsActive })
                .ToListAsync(cancellationToken);

            return Results.Ok(members);
        })
        .RequireAuthorization()
        .WithName("ListTeamMembers")
        .WithSummary("Lista os membros da equipe do estabelecimento autenticado.");

        team.MapGet("/invitations", async (IdentityDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var pending = await dbContext.TeamInvitations
                .Where(i => i.AcceptedAtUtc == null)
                .OrderBy(i => i.CreatedAtUtc)
                .Select(i => new { id = i.Id.Value, email = i.Email.Value, role = i.Role.ToString(), i.ExpiresAtUtc })
                .ToListAsync(cancellationToken);

            return Results.Ok(pending);
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("ListPendingInvitations")
        .WithSummary("Lista os convites pendentes do estabelecimento.");
    }

    private static void SetRefreshTokenCookie(HttpContext httpContext, AuthTokensResult tokens)
    {
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = tokens.RefreshTokenExpiresAtUtc,
            Path = "/api/auth",
        });
    }

    // O refresh token NUNCA aparece no corpo da resposta — so no cookie
    // HttpOnly. O access token vive em memoria no frontend (nunca localStorage).
    private static object ToResponse(AuthTokensResult tokens) => new
    {
        accessToken = tokens.AccessToken,
        expiresAtUtc = tokens.AccessTokenExpiresAtUtc,
    };

    private sealed record RegisterRequest(Guid TenantId, string Email, string Password, string FullName);

    private sealed record LoginRequest(Guid TenantId, string Email, string Password);

    private sealed record InviteTeamMemberRequest(string Email, UserRole Role);

    private sealed record AcceptInvitationRequest(string FullName, string Password);
}
