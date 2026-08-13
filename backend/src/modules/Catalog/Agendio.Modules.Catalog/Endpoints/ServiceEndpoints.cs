using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Catalog.Application.CreateService;
using Agendio.Modules.Catalog.Application.DeactivateService;
using Agendio.Modules.Catalog.Application.GetServiceById;
using Agendio.Modules.Catalog.Application.ListServices;
using Agendio.Modules.Catalog.Application.UpdateService;
using Agendio.Modules.Catalog.Application.UploadServiceImage;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Catalog.Endpoints;

public sealed class ServiceEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/services").WithTags("Catalog").RequireAuthorization();

        // page/pageSize precisam de "= valor" na assinatura: parametro de
        // query string sem default no delegate e OBRIGATORIO no Minimal API.
        group.MapGet("/", async (string? search, IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new ListServicesQuery(search, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListServices")
        .WithSummary("Lista os servicos do estabelecimento, com busca por nome e paginacao.");

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetServiceByIdQuery(id), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetServiceById")
        .WithSummary("Busca um servico pelo Id.");

        group.MapPost("/", async (CreateServiceRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new CreateServiceCommand(
                request.Name, request.Description, request.DurationMinutes, request.Price,
                request.Currency ?? "BRL", request.Category, request.DisplayOrder);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/services/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("CreateService")
        .WithSummary("Cadastra um novo servico.");

        group.MapPut("/{id:guid}", async (Guid id, UpdateServiceRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateServiceCommand(
                id, request.Name, request.Description, request.DurationMinutes,
                request.Price, request.Currency ?? "BRL", request.Category, request.DisplayOrder);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("UpdateService")
        .WithSummary("Atualiza os dados de um servico.");

        group.MapPatch("/{id:guid}/status", async (Guid id, SetActiveStatusRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetServiceActiveStatusCommand(id, request.IsActive), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SetServiceActiveStatus")
        .WithSummary("Ativa ou desativa um servico.");

        group.MapPost("/{id:guid}/image", async (Guid id, IFormFile file, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await using var contentStream = new MemoryStream();
            await file.CopyToAsync(contentStream, cancellationToken);

            var command = new UploadServiceImageCommand(id, contentStream.ToArray(), file.ContentType);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.Ok(new { imageUrl = result.Value }) : result.Error.ToProblemResult();
        })
        .DisableAntiforgery()
        .WithName("UploadServiceImage")
        .WithSummary("Faz upload da imagem do servico (PNG/JPEG/WEBP).");
    }

    private sealed record CreateServiceRequest(
        string Name, string? Description, int DurationMinutes, decimal Price, string? Currency, string? Category, int DisplayOrder = 0);

    private sealed record UpdateServiceRequest(
        string Name, string? Description, int DurationMinutes, decimal Price, string? Currency, string? Category, int DisplayOrder);

    private sealed record SetActiveStatusRequest(bool IsActive);
}
