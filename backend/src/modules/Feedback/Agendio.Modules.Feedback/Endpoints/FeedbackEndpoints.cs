using System.Security.Claims;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Feedback.Application.SubmitFeedback;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Feedback.Endpoints;

public sealed class FeedbackEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/feedback").WithTags("Feedback");

        group.MapPost("/", async (HttpContext httpContext, SubmitFeedbackRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new SubmitFeedbackCommand(GetUserId(httpContext), request.Subject, request.Body);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        // Qualquer papel autenticado pode enviar feedback (Owner ou Staff) —
        // pedido explicito do usuario (2026-09-05), diferente de Empresa/Plano
        // (Owner-only). Grava no banco e notifica por e-mail (best-effort, ver
        // SubmitFeedbackCommandHandler.TryNotifyAsync).
        .RequireAuthorization()
        .WithName("SubmitFeedback")
        .WithSummary("Registra um feedback livre do usuario autenticado sobre o sistema.");
    }

    // Mesmo padrao de IdentityEndpoints.GetUserId — claim do JWT (ver
    // AuthTokenIssuer, ClaimTypes.NameIdentifier = user.Id).
    private static Guid GetUserId(HttpContext httpContext) =>
        Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private sealed record SubmitFeedbackRequest(string Subject, string Body);
}
