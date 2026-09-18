using System.Security.Claims;
using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Assistant.Application.AskAssistant;
using Agendio.Modules.Assistant.Knowledge;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Assistant.Endpoints;

public sealed class AssistantEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/assistant").WithTags("Assistant").RequireAuthorization();

        group.MapPost("/ask", async (AskAssistantRequest request, HttpContext httpContext, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var callerRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? FeatureCatalog.RoleAny;
            var callerUserId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var command = new AskAssistantCommand(request.Question, request.History ?? [], callerRole, callerUserId);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireRateLimiting("ai-assistant")
        .WithName("AskAssistant")
        .WithSummary("Pergunta em linguagem natural sobre os dados agregados do proprio estabelecimento (financeiro, agendamentos, estoque, avaliacoes) ou sobre como usar o sistema.");

        group.MapGet("/features", (HttpContext httpContext) =>
        {
            var callerRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? FeatureCatalog.RoleAny;
            return Results.Ok(FeatureCatalog.ForRole(callerRole));
        })
        .WithName("ListAssistantFeatures")
        .WithSummary("Catalogo de telas/funcionalidades do sistema, filtrado pelo papel do usuario -- usado pelas sugestoes do widget do assistente.");
    }

    private sealed record AskAssistantRequest(string Question, IReadOnlyList<AssistantChatMessageDto>? History);
}
