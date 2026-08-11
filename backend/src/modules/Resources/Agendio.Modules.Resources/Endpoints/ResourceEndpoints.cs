using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Resources.Application.CreateResource;
using Agendio.Modules.Resources.Application.DeactivateResource;
using Agendio.Modules.Resources.Application.GetResourceById;
using Agendio.Modules.Resources.Application.ListResources;
using Agendio.Modules.Resources.Application.SetWorkingHours;
using Agendio.Modules.Resources.Application.UpdateResource;
using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Resources.Endpoints;

public sealed class ResourceEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/resources").WithTags("Resources").RequireAuthorization();

        // page/pageSize precisam de "= valor" na assinatura: parametro de
        // query string sem default no delegate e OBRIGATORIO no Minimal API.
        group.MapGet("/", async (string? search, IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new ListResourcesQuery(search, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListResources")
        .WithSummary("Lista os recursos do estabelecimento, com busca por nome e paginacao.");

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetResourceByIdQuery(id), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetResourceById")
        .WithSummary("Busca um recurso pelo Id, incluindo os horarios de trabalho.");

        group.MapPost("/", async (CreateResourceRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new CreateResourceCommand(request.Name, request.Type, request.Capacity, request.Description, request.UnitId);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/resources/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("CreateResource")
        .WithSummary("Cadastra um novo recurso (pessoa, sala ou equipamento).");

        group.MapPut("/{id:guid}", async (Guid id, UpdateResourceRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateResourceCommand(id, request.Name, request.Type, request.Capacity, request.Description, request.UnitId);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("UpdateResource")
        .WithSummary("Atualiza os dados de um recurso.");

        group.MapPatch("/{id:guid}/status", async (Guid id, SetActiveStatusRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetResourceActiveStatusCommand(id, request.IsActive), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SetResourceActiveStatus")
        .WithSummary("Ativa ou desativa um recurso.");

        group.MapPut("/{id:guid}/working-hours", async (Guid id, SetWorkingHoursRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var entries = request.Entries.Select(e => new WorkingHourEntryDto(e.DayOfWeek, e.StartTime, e.EndTime)).ToList();
            var result = await dispatcher.Send(new SetResourceWorkingHoursCommand(id, entries), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SetResourceWorkingHours")
        .WithSummary("Substitui a semana inteira de horarios de trabalho do recurso.");
    }

    private sealed record CreateResourceRequest(string Name, ResourceType Type, int Capacity, string? Description, Guid? UnitId);

    private sealed record UpdateResourceRequest(string Name, ResourceType Type, int Capacity, string? Description, Guid? UnitId);

    private sealed record SetActiveStatusRequest(bool IsActive);

    private sealed record WorkingHourEntryRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    private sealed record SetWorkingHoursRequest(IReadOnlyList<WorkingHourEntryRequest> Entries);
}
