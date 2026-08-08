using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Estoque.Application.CreateProduct;
using Agendio.Modules.Estoque.Application.GetInventorySummary;
using Agendio.Modules.Estoque.Application.GetProductById;
using Agendio.Modules.Estoque.Application.ListProducts;
using Agendio.Modules.Estoque.Application.ListStockMovements;
using Agendio.Modules.Estoque.Application.RegisterStockMovement;
using Agendio.Modules.Estoque.Application.SetProductActiveStatus;
using Agendio.Modules.Estoque.Application.UpdateProduct;
using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Estoque.Endpoints;

public sealed class EstoqueEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/estoque").WithTags("Estoque").RequireAuthorization();

        group.MapGet("/resumo", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetInventorySummaryQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetInventorySummary")
        .WithSummary("Resumo do estoque atual: produtos ativos, estoque baixo e valor total em estoque.");

        group.MapGet("/produtos", async (
            string? search, bool? isActive, IDispatcher dispatcher, CancellationToken cancellationToken,
            bool lowStockOnly = false, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new ListProductsQuery(search, isActive, lowStockOnly, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListProducts")
        .WithSummary("Lista os produtos, com filtro por busca, status e estoque baixo.");

        group.MapGet("/produtos/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetProductByIdQuery(id), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetProductById")
        .WithSummary("Detalha um produto.");

        group.MapPost("/produtos", async (CreateProductRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new CreateProductCommand(
                request.Name, request.Sku, request.Description, request.QuantityInStock, request.MinimumStock, request.SalePrice,
                request.Currency);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/estoque/produtos/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("CreateProduct")
        .WithSummary("Cadastra um novo produto.");

        group.MapPut("/produtos/{id:guid}", async (
            Guid id, UpdateProductRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateProductCommand(
                id, request.Name, request.Sku, request.Description, request.MinimumStock, request.SalePrice, request.Currency);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("UpdateProduct")
        .WithSummary("Atualiza os dados de um produto (nao altera a quantidade em estoque).");

        group.MapPatch("/produtos/{id:guid}/status", async (
            Guid id, SetActiveStatusRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetProductActiveStatusCommand(id, request.IsActive), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SetProductActiveStatus")
        .WithSummary("Ativa ou desativa um produto.");

        group.MapPost("/produtos/{id:guid}/movimentacoes", async (
            Guid id, RegisterStockMovementRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new RegisterStockMovementCommand(
                id, request.Type, request.Quantity, request.Reason, request.Notes, request.OccurredAtUtc);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/estoque/movimentacoes/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("RegisterStockMovement")
        .WithSummary("Registra uma entrada ou saida manual de estoque.");

        group.MapGet("/movimentacoes", async (
            Guid? productId, StockMovementType? type, StockMovementReason? reason, DateOnly? from, DateOnly? to,
            IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(
                new ListStockMovementsQuery(productId, type, reason, from, to, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListStockMovements")
        .WithSummary("Historico de movimentacoes de estoque, com filtro por produto, tipo, motivo e periodo.");
    }

    private sealed record CreateProductRequest(
        string Name, string? Sku, string? Description, int QuantityInStock, int MinimumStock, decimal? SalePrice, string? Currency);

    private sealed record UpdateProductRequest(string Name, string? Sku, string? Description, int MinimumStock, decimal? SalePrice, string? Currency);

    private sealed record SetActiveStatusRequest(bool IsActive);

    private sealed record RegisterStockMovementRequest(
        StockMovementType Type, int Quantity, StockMovementReason Reason, string? Notes, DateTimeOffset? OccurredAtUtc);
}
