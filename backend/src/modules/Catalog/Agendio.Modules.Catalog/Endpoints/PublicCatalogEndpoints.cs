using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Catalog.Application.PublicListServices;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Catalog.Endpoints;

/// <summary>
/// Superficie anonima consumida pelo portal publico do cliente (Sprint 4) —
/// nunca .RequireAuthorization(), o tenant vem explicito na rota (ver
/// IHasExplicitTenant), nao de um JWT que o visitante ainda nao tem.
/// </summary>
public sealed class PublicCatalogEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/public/tenants/{tenantId:guid}/services").WithTags("Public Catalog");

        group.MapGet("/", async (Guid tenantId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new PublicListServicesQuery(tenantId), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("PublicListServices")
        .WithSummary("Lista os servicos ativos de um tenant para o portal publico de agendamento.");
    }
}
