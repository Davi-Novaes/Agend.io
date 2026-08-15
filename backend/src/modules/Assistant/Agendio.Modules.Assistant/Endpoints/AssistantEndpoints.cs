using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Assistant.Application.AskAssistant;
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

        group.MapPost("/ask", async (AskAssistantRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new AskAssistantCommand(request.Question, request.History ?? []);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireRateLimiting("ai-assistant")
        .WithName("AskAssistant")
        .WithSummary("Pergunta em linguagem natural sobre os dados agregados do proprio estabelecimento (financeiro, agendamentos, estoque, avaliacoes).");
    }

    private sealed record AskAssistantRequest(string Question, IReadOnlyList<AssistantChatMessageDto>? History);
}
