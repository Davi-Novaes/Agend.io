using System.Security.Claims;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Identity.Application;
using Agendio.Modules.Identity.Application.AcceptInvitation;
using Agendio.Modules.Identity.Application.ChangePassword;
using Agendio.Modules.Identity.Application.ConfirmEmail;
using Agendio.Modules.Identity.Application.DisableMfa;
using Agendio.Modules.Identity.Application.EnableMfa;
using Agendio.Modules.Identity.Application.ForgotPassword;
using Agendio.Modules.Identity.Application.GetMfaStatus;
using Agendio.Modules.Identity.Application.GetMySecurityActivity;
using Agendio.Modules.Identity.Application.GetMyProfile;
using Agendio.Modules.Identity.Application.InviteTeamMember;
using Agendio.Modules.Identity.Application.Login;
using Agendio.Modules.Identity.Application.Logout;
using Agendio.Modules.Identity.Application.LogoutAllSessions;
using Agendio.Modules.Identity.Application.RefreshAccessToken;
using Agendio.Modules.Identity.Application.RegisterUser;
using Agendio.Modules.Identity.Application.ResendConfirmationEmail;
using Agendio.Modules.Identity.Application.ResetPassword;
using Agendio.Modules.Identity.Application.SetupMfa;
using Agendio.Modules.Identity.Application.UpdateMyProfile;
using Agendio.Modules.Identity.Application.UploadUserAvatar;
using Agendio.Modules.Identity.Application.VerifyMfa;
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
            var command = new RegisterUserCommand(
                request.TenantId, request.Email, request.Password, request.FullName, request.Phone, request.CpfCnpj, request.TermsAccepted);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/tenants/{request.TenantId}", new
                {
                    id = result.Value.UserId,
                    onboardingToken = result.Value.OnboardingToken,
                    onboardingTokenExpiresAtUtc = result.Value.OnboardingTokenExpiresAtUtc,
                })
                : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("RegisterUser")
        .WithSummary("Registra o primeiro usuario (dono) de um estabelecimento.");

        group.MapPost("/confirm-email", async (ConfirmEmailRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new ConfirmEmailCommand(request.Token), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("ConfirmEmail")
        .WithSummary("Confirma o e-mail do usuario a partir do token enviado por e-mail no cadastro.");

        group.MapPost("/resend-confirmation", async (ResendConfirmationRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new ResendConfirmationEmailCommand(request.TenantId, request.Email);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        // Sempre 204, exista ou nao o e-mail — evita enumeracao de contas (ver Handler).
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("ResendConfirmationEmail")
        .WithSummary("Reenvia o e-mail de confirmacao de cadastro, se pendente.");

        group.MapPost("/login", async (LoginRequest request, IDispatcher dispatcher, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var command = new LoginCommand(request.TenantId, request.Email, request.Password);
            var result = await dispatcher.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.ToProblemResult();
            }

            return result.Value switch
            {
                LoginSuccess success => CompleteLogin(httpContext, success.Tokens),
                LoginMfaChallenge challenge => Results.Ok(new
                {
                    mfaRequired = true,
                    mfaChallengeToken = challenge.ChallengeToken,
                    expiresAtUtc = challenge.ExpiresAtUtc,
                }),
                _ => throw new InvalidOperationException($"LoginResult inesperado: {result.Value.GetType().Name}."),
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("Login");

        group.MapPost("/mfa/verify", async (VerifyMfaRequest request, IDispatcher dispatcher, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var command = new VerifyMfaCommand(request.MfaChallengeToken, request.Code);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? CompleteLogin(httpContext, result.Value) : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("VerifyMfa")
        .WithSummary("Segunda etapa do login quando MFA esta habilitado: confirma o codigo TOTP (ou de recuperacao) e emite os tokens.");

        group.MapGet("/me", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetMyProfileQuery(GetUserId(httpContext)), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .WithName("GetMyProfile")
        .WithSummary("Perfil basico do usuario autenticado (nome, e-mail, foto, telefone) — usado pelo avatar do AppHeader e pela tela Minha conta.");

        group.MapPut("/me", async (UpdateMyProfileRequest request, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateMyProfileCommand(GetUserId(httpContext), request.FullName, request.Phone);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .WithName("UpdateMyProfile")
        .WithSummary("Atualiza nome e telefone do usuario autenticado. E-mail fica de fora — mudar exigiria um fluxo de confirmacao que ainda nao existe.");

        group.MapPost("/me/avatar", async (IFormFile file, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await using var contentStream = new MemoryStream();
            await file.CopyToAsync(contentStream, cancellationToken);

            var command = new UploadUserAvatarCommand(GetUserId(httpContext), contentStream.ToArray(), file.ContentType);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok(new { avatarUrl = result.Value }) : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithName("UploadUserAvatar")
        .WithSummary("Faz upload da foto de perfil do usuario autenticado (PNG/JPEG/WEBP).");

        group.MapGet("/mfa/status", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetMfaStatusQuery(GetUserId(httpContext)), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .WithName("GetMfaStatus")
        .WithSummary("Diz se o usuario autenticado tem MFA habilitado — usado pela tela de Configuracoes/Seguranca.");

        group.MapGet("/security/activity", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetMySecurityActivityQuery(GetUserId(httpContext)), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .WithName("GetMySecurityActivity")
        .WithSummary("Ultimos eventos de seguranca (login, MFA, etc) da PROPRIA conta do usuario autenticado — usado pela tela de Configuracoes/Seguranca.");

        group.MapPost("/mfa/setup", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetupMfaCommand(GetUserId(httpContext)), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .WithName("SetupMfa")
        .WithSummary("Gera um novo segredo TOTP e a URI de provisionamento (QR code) — nada e persistido ate EnableMfa confirmar.");

        group.MapPost("/mfa/enable", async (EnableMfaRequest request, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new EnableMfaCommand(GetUserId(httpContext), request.Secret, request.Code);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok(new { recoveryCodes = result.Value }) : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .WithName("EnableMfa")
        .WithSummary("Confirma o codigo TOTP e ativa MFA — devolve os codigos de recuperacao em texto puro, unica vez que aparecem.");

        group.MapPost("/mfa/disable", async (DisableMfaRequest request, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new DisableMfaCommand(GetUserId(httpContext), request.Password, request.Code);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .WithName("DisableMfa")
        .WithSummary("Desliga MFA — exige senha e um codigo (TOTP ou recuperacao) validos.");

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new ForgotPasswordCommand(request.TenantId, request.Email);
            await dispatcher.Send(command, cancellationToken);

            // Sempre 204, exista ou nao o e-mail — mesmo raciocinio de resend-confirmation.
            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("ForgotPassword")
        .WithSummary("Pede o e-mail de recuperacao de senha. Sempre responde 204, exista ou nao a conta.");

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new ResetPasswordCommand(request.Token, request.NewPassword);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("ResetPassword")
        .WithSummary("Define uma nova senha a partir do token recebido por e-mail e revoga todas as sessoes ativas.");

        group.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new ChangePasswordCommand(GetUserId(httpContext), request.CurrentPassword, request.NewPassword);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .RequireRateLimiting("auth")
        .WithName("ChangePassword")
        .WithSummary("Troca a senha do usuario autenticado (exige a senha atual) e revoga todas as sessoes ativas.");

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
                DeleteRefreshTokenCookie(httpContext);
                return result.Error.ToProblemResult();
            }

            SetRefreshTokenCookie(httpContext, result.Value);
            return Results.Ok(ToResponse(result.Value));
        })
        .AllowAnonymous()
        .WithName("RefreshAccessToken");

        group.MapPost("/logout", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var refreshToken = httpContext.Request.Cookies[RefreshTokenCookieName];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await dispatcher.Send(new LogoutCommand(refreshToken), cancellationToken);
            }

            DeleteRefreshTokenCookie(httpContext);
            return Results.NoContent();
        })
        // Publica de proposito: se o access token ja expirou, o frontend ainda
        // precisa conseguir limpar a sessao (cookie + revogar o refresh token).
        .AllowAnonymous()
        .WithName("Logout")
        .WithSummary("Revoga o refresh token da sessao atual e limpa o cookie.");

        group.MapPost("/logout-all", async (HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new LogoutAllSessionsCommand(GetUserId(httpContext)), cancellationToken);

            DeleteRefreshTokenCookie(httpContext);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization()
        .RequireRateLimiting("auth")
        .WithName("LogoutAllSessions")
        .WithSummary("Revoga TODOS os refresh tokens do usuario (todos os dispositivos/abas), nao so a sessao atual.");

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

    private static IResult CompleteLogin(HttpContext httpContext, AuthTokensResult tokens)
    {
        SetRefreshTokenCookie(httpContext, tokens);
        return Results.Ok(ToResponse(tokens));
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

    // Path precisa bater exatamente com o usado em SetRefreshTokenCookie —
    // Response.Cookies.Delete sem essa opcao nao remove o cookie de fato
    // (o navegador trata como um Set-Cookie de um path diferente).
    private static void DeleteRefreshTokenCookie(HttpContext httpContext) =>
        httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/auth" });

    // O refresh token NUNCA aparece no corpo da resposta — so no cookie
    // HttpOnly. O access token vive em memoria no frontend (nunca localStorage).
    // mfaRequired:false aqui deixa o frontend tratar a resposta de /login e de
    // /mfa/verify com o MESMO tipo (ver LoginMfaChallenge no endpoint /login).
    private static object ToResponse(AuthTokensResult tokens) => new
    {
        mfaRequired = false,
        accessToken = tokens.AccessToken,
        expiresAtUtc = tokens.AccessTokenExpiresAtUtc,
    };

    // UserId sempre presente nas rotas com .RequireAuthorization(): a claim vem
    // do JWT emitido em AuthTokenIssuer (ClaimTypes.NameIdentifier = user.Id).
    private static Guid GetUserId(HttpContext httpContext) =>
        Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private sealed record UpdateMyProfileRequest(string FullName, string? Phone);

    private sealed record RegisterRequest(Guid TenantId, string Email, string Password, string FullName, string Phone, string CpfCnpj, bool TermsAccepted);

    private sealed record ConfirmEmailRequest(string Token);

    private sealed record ResendConfirmationRequest(Guid TenantId, string Email);

    private sealed record ForgotPasswordRequest(Guid TenantId, string Email);

    private sealed record ResetPasswordRequest(string Token, string NewPassword);

    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private sealed record LoginRequest(Guid TenantId, string Email, string Password);

    private sealed record VerifyMfaRequest(string MfaChallengeToken, string Code);

    private sealed record EnableMfaRequest(string Secret, string Code);

    private sealed record DisableMfaRequest(string Password, string Code);

    private sealed record InviteTeamMemberRequest(string Email, UserRole Role);

    private sealed record AcceptInvitationRequest(string FullName, string Password);
}
