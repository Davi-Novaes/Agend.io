using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Financeiro.Application.CancelAccountPayable;
using Agendio.Modules.Financeiro.Application.CreateAccountPayable;
using Agendio.Modules.Financeiro.Application.DeactivateCommissionRule;
using Agendio.Modules.Financeiro.Application.GetCashFlowSummary;
using Agendio.Modules.Financeiro.Application.ListAccountsPayable;
using Agendio.Modules.Financeiro.Application.ListAccountsReceivable;
using Agendio.Modules.Financeiro.Application.ListCommissionRules;
using Agendio.Modules.Financeiro.Application.MarkAccountPayablePaid;
using Agendio.Modules.Financeiro.Application.MarkAccountReceivableReceived;
using Agendio.Modules.Financeiro.Application.UpsertCommissionRule;
using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendio.Modules.Financeiro.Endpoints;

public sealed class FinanceiroEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/financeiro").WithTags("Financeiro").RequireAuthorization();

        group.MapGet("/contas-a-receber", async (
            AccountReceivableStatus? status, DateOnly? from, DateOnly? to, IDispatcher dispatcher, CancellationToken cancellationToken,
            int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new ListAccountsReceivableQuery(status, from, to, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListAccountsReceivable")
        .WithSummary("Lista as contas a receber, com filtro por status e periodo.");

        group.MapPatch("/contas-a-receber/{id:guid}/receber", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new MarkAccountReceivableReceivedCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        // "Owner" e um literal (nao Identity.Domain.UserRole) de proposito: um
        // modulo nunca referencia outro, so .Contracts — e nao vale a pena
        // criar um contrato so por causa de uma string de role.
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("MarkAccountReceivableReceived")
        .WithSummary("Confirma o recebimento de uma conta a receber.");

        group.MapGet("/contas-a-pagar", async (
            AccountPayableStatus? status, ExpenseCategory? category, DateOnly? from, DateOnly? to,
            IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await dispatcher.Query(new ListAccountsPayableQuery(status, category, from, to, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListAccountsPayable")
        .WithSummary("Lista as contas a pagar, com filtro por status, categoria e periodo.");

        group.MapPost("/contas-a-pagar", async (CreateAccountPayableRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new CreateAccountPayableCommand(request.Description, request.Amount, request.DueDate, request.Category);
            var result = await dispatcher.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/financeiro/contas-a-pagar/{result.Value}", new { id = result.Value })
                : result.Error.ToProblemResult();
        })
        .WithName("CreateAccountPayable")
        .WithSummary("Lanca uma despesa manual (aluguel, insumos, etc.).");

        group.MapPatch("/contas-a-pagar/{id:guid}/pagar", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new MarkAccountPayablePaidCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("MarkAccountPayablePaid")
        .WithSummary("Confirma o pagamento de uma conta a pagar.");

        group.MapPatch("/contas-a-pagar/{id:guid}/cancelar", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new CancelAccountPayableCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("CancelAccountPayable")
        .WithSummary("Cancela uma conta a pagar pendente.");

        group.MapGet("/comissoes", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new ListCommissionRulesQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("ListCommissionRules")
        .WithSummary("Lista os profissionais (recursos tipo Pessoa) e a regra de comissao de cada um, quando configurada.");

        group.MapPut("/comissoes/{resourceId:guid}", async (
            Guid resourceId, UpsertCommissionRuleRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpsertCommissionRuleCommand(resourceId, request.CalculationType, request.Value);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("UpsertCommissionRule")
        .WithSummary("Cria ou atualiza a regra de comissao de um profissional.");

        group.MapDelete("/comissoes/{resourceId:guid}", async (Guid resourceId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new DeactivateCommissionRuleCommand(resourceId), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .RequireAuthorization(policy => policy.RequireRole("Owner"))
        .WithName("DeactivateCommissionRule")
        .WithSummary("Remove a comissao configurada de um profissional.");

        group.MapGet("/fluxo-de-caixa", async (DateOnly from, DateOnly to, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetCashFlowSummaryQuery(from, to), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetCashFlowSummary")
        .WithSummary("Resumo do fluxo de caixa realizado (entradas x saidas) num periodo, com evolucao mensal e categorias de despesa.");
    }

    private sealed record CreateAccountPayableRequest(string Description, decimal Amount, DateOnly DueDate, ExpenseCategory Category);

    private sealed record UpsertCommissionRuleRequest(CommissionCalculationType CalculationType, decimal Value);
}
