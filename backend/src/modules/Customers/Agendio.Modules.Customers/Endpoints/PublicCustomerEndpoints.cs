using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Customers.Application.PublicGetLoyaltyStatus;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Customers.Endpoints;

/// <summary>Superficie anonima consumida pelo portal publico do cliente — ver PublicCatalogEndpoints.</summary>
public sealed class PublicCustomerEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/public/tenants/{tenantId:guid}/loyalty").WithTags("Public Loyalty");

        group.MapGet("/", async (Guid tenantId, string email, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new PublicGetLoyaltyStatusQuery(tenantId, email), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("PublicGetLoyaltyStatus")
        .WithSummary("Consulta o saldo de pontos de fidelidade de um cliente pelo e-mail, sem exigir login (Fase 11).");
    }
}
