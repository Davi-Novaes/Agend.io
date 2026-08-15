using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Marketing.Application.ListCampaigns;
using Agendio.Modules.Marketing.Application.SendCampaign;
using Agendio.Modules.Marketing.Domain;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Marketing.Endpoints;

public sealed class MarketingEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/marketing").WithTags("Marketing").RequireAuthorization();

        group.MapPost("/campanhas", async (SendCampaignRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new SendCampaignCommand(request.Subject, request.Body, request.Channel, request.TargetSegment);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/marketing/campanhas/{result.Value.CampaignId}", new { id = result.Value.CampaignId, result.Value.RecipientCount })
                : result.Error.ToProblemResult();
        })
        .WithName("SendCampaign")
        .WithSummary("Envia uma campanha (e-mail ou WhatsApp) para os clientes ativos do publico-alvo selecionado.");

        group.MapGet("/campanhas", async (IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new ListCampaignsQuery(page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListCampaigns")
        .WithSummary("Historico de campanhas enviadas.");
    }

    private sealed record SendCampaignRequest(string Subject, string Body, CampaignChannel Channel, CustomerSegment? TargetSegment);
}
