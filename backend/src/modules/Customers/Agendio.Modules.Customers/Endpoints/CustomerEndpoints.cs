using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Customers.Application.CreateCustomer;
using Agendio.Modules.Customers.Application.DeactivateCustomer;
using Agendio.Modules.Customers.Application.GetCustomerAuditLog;
using Agendio.Modules.Customers.Application.GetCustomerById;
using Agendio.Modules.Customers.Application.ImportCustomersFromCsv;
using Agendio.Modules.Customers.Application.ListCustomers;
using Agendio.Modules.Customers.Application.RedeemCustomerLoyaltyReward;
using Agendio.Modules.Customers.Application.SendCustomerMessage;
using Agendio.Modules.Customers.Application.UpdateCustomer;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Customers.Endpoints;

public sealed class CustomerEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/customers").WithTags("Customers").RequireAuthorization();

        // page/pageSize precisam de "= valor" aqui (nao so no record da query):
        // um parametro de query string sem default no delegate e OBRIGATORIO
        // no Minimal API — ausente na URL vira 400, nao o default do C#.
        group.MapGet("/", async (string? search, CustomerSegment? segment, IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new ListCustomersQuery(search, segment, page, pageSize), cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListCustomers")
        .WithSummary("Lista os clientes do estabelecimento, com busca por nome, filtro por segmento (Fase 9) e paginacao.");

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetCustomerByIdQuery(id), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetCustomerById")
        .WithSummary("Busca um cliente pelo Id.");

        group.MapPost("/", async (CreateCustomerRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new CreateCustomerCommand(
                request.FullName, request.Email, request.Phone, request.Notes, request.DateOfBirth, request.CustomData,
                request.Cpf, request.HealthNotes);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/customers/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("CreateCustomer")
        .WithSummary("Cadastra um novo cliente.");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCustomerRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateCustomerCommand(
                id, request.FullName, request.Email, request.Phone, request.Notes, request.DateOfBirth, request.CustomData,
                request.Cpf, request.HealthNotes);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("UpdateCustomer")
        .WithSummary("Atualiza os dados de um cliente.");

        group.MapPatch("/{id:guid}/status", async (Guid id, SetActiveStatusRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SetCustomerActiveStatusCommand(id, request.IsActive), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SetCustomerActiveStatus")
        .WithSummary("Ativa ou desativa um cliente (soft toggle, nao apaga o historico).");

        group.MapGet("/{id:guid}/audit-log", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new GetCustomerAuditLogQuery(id, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetCustomerAuditLog")
        .WithSummary("Trilha de auditoria do cliente: quem criou/alterou o cadastro, quando, e o que mudou.");

        group.MapPost("/{id:guid}/send-message", async (Guid id, SendCustomerMessageRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new SendCustomerMessageCommand(id, request.Subject, request.Body), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("SendCustomerMessage")
        .WithSummary("Envia uma mensagem avulsa por e-mail a um cliente especifico (Fase 10 — recuperacao de clientes).");

        group.MapPost("/{id:guid}/redeem-loyalty-reward", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new RedeemCustomerLoyaltyRewardCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("RedeemCustomerLoyaltyReward")
        .WithSummary("Resgata a recompensa de fidelidade de um cliente, debitando os pontos necessarios (Fase 11).");

        group.MapPost("/import", async (IFormFile file, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await using var contentStream = new MemoryStream();
            await file.CopyToAsync(contentStream, cancellationToken);

            var result = await dispatcher.Send(new ImportCustomersFromCsvCommand(contentStream.ToArray()), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .DisableAntiforgery()
        .WithName("ImportCustomersFromCsv")
        .WithSummary("Importa clientes em lote a partir de um arquivo CSV (colunas: FullName, Email, Phone, Notes).");
    }

    private sealed record CreateCustomerRequest(
        string FullName, string? Email, string? Phone, string? Notes, DateOnly? DateOfBirth, Dictionary<string, string>? CustomData,
        string? Cpf = null, string? HealthNotes = null);

    private sealed record UpdateCustomerRequest(
        string FullName, string? Email, string? Phone, string? Notes, DateOnly? DateOfBirth, Dictionary<string, string>? CustomData,
        string? Cpf = null, string? HealthNotes = null);

    private sealed record SetActiveStatusRequest(bool IsActive);

    private sealed record SendCustomerMessageRequest(string Subject, string Body);
}
