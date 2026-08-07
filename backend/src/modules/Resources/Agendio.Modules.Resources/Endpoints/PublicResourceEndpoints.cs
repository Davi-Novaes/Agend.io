using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Resources.Application.PublicListResources;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Resources.Endpoints;

/// <summary>Superficie anonima consumida pelo portal publico do cliente (Sprint 4) — ver PublicCatalogEndpoints.</summary>
public sealed class PublicResourceEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/public/tenants/{tenantId:guid}/resources").WithTags("Public Resources");

        group.MapGet("/", async (Guid tenantId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new PublicListResourcesQuery(tenantId), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("PublicListResources")
        .WithSummary("Lista os recursos ativos de um tenant para o portal publico de agendamento.");
    }
}
