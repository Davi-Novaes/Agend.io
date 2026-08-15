using System.Text.Json;
using Agendio.Infrastructure.AiAssistant;
using Agendio.Modules.Estoque.Contracts;
using Agendio.Modules.Financeiro.Contracts;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Assistant.Application.AskAssistant;

public sealed class AskAssistantCommandHandler(
    IAiChatClient chatClient,
    IOptions<AiAssistantOptions> aiOptions,
    IFinanceSummaryLookupService financeLookup,
    IAppointmentStatsLookupService appointmentStatsLookup,
    IReviewsSummaryLookupService reviewsLookup,
    IInventorySummaryLookupService inventoryLookup,
    ITenantLookupService tenantLookup,
    ITenantContext tenantContext,
    IClock clock) : ICommandHandler<AskAssistantCommand, AskAssistantResult>
{
    // Limite de idas-e-voltas de ferramenta por pergunta — protege contra um
    // loop indefinido (custo por chamada de IA) sem cortar perguntas legitimas
    // que precisam de 2-3 ferramentas (ex: "faturamento e estoque baixo").
    private const int MaxToolIterations = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<AskAssistantResult>> Handle(AskAssistantCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(aiOptions.Value.ApiKey))
        {
            return Result.Failure<AskAssistantResult>(Error.Failure(
                "Assistant.NotConfigured", "O assistente ainda nao esta configurado nesta instalacao do Agend.io."));
        }

        var tenant = await tenantLookup.FindByIdAsync(tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<AskAssistantResult>(Error.Failure("Assistant.TenantNotFound", "Estabelecimento nao encontrado."));
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, ResolveTimeZone(tenant.TimeZoneId)).DateTime);
        var systemPrompt = BuildSystemPrompt(tenant.Name, today);
        var tools = BuildTools();

        var messages = request.History
            .Select(m => new AiChatMessage { Role = m.Role == "assistant" ? AiChatRole.Assistant : AiChatRole.User, Text = m.Text })
            .ToList();
        messages.Add(new AiChatMessage { Role = AiChatRole.User, Text = request.Question });

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            AiChatResult response;
            try
            {
                response = await chatClient.SendAsync(new AiChatRequest(systemPrompt, messages, tools), cancellationToken);
            }
            catch (Exception)
            {
                return Result.Failure<AskAssistantResult>(Error.Failure(
                    "Assistant.Unavailable", "Nao foi possivel falar com o assistente agora. Tente novamente em instantes."));
            }

            if (response.Kind == AiChatResultKind.Text)
            {
                return Result.Success(new AskAssistantResult(response.Text ?? string.Empty));
            }

            var toolCalls = response.ToolCalls!;
            messages.Add(new AiChatMessage { Role = AiChatRole.Assistant, ToolCalls = toolCalls });

            foreach (var toolCall in toolCalls)
            {
                var toolResultJson = await ExecuteToolAsync(toolCall, cancellationToken);
                messages.Add(new AiChatMessage { Role = AiChatRole.Tool, ToolCallId = toolCall.Id, Text = toolResultJson });
            }
        }

        return Result.Failure<AskAssistantResult>(Error.Failure(
            "Assistant.TooManySteps", "Nao consegui montar uma resposta completa para essa pergunta. Tente reformular de um jeito mais simples."));
    }

    private async Task<string> ExecuteToolAsync(AiToolCall toolCall, CancellationToken cancellationToken)
    {
        try
        {
            using var arguments = JsonDocument.Parse(toolCall.ArgumentsJson is { Length: > 0 } json ? json : "{}");
            var root = arguments.RootElement;

            object result = toolCall.Name switch
            {
                "get_cash_flow_summary" => await financeLookup.GetCashFlowSummaryAsync(ReadDate(root, "from"), ReadDate(root, "to"), cancellationToken),
                "get_commission_report" => await financeLookup.GetCommissionReportAsync(ReadDate(root, "from"), ReadDate(root, "to"), cancellationToken),
                "get_appointment_stats" => await appointmentStatsLookup.GetStatsAsync(ReadDate(root, "from"), ReadDate(root, "to"), cancellationToken),
                "get_reviews_summary" => await reviewsLookup.GetSummaryAsync(ReadDate(root, "from"), ReadDate(root, "to"), cancellationToken),
                "get_inventory_summary" => await inventoryLookup.GetSummaryAsync(cancellationToken),
                _ => new { error = $"Ferramenta desconhecida: {toolCall.Name}" },
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException)
        {
            // Devolve o erro pro proprio modelo como resultado da ferramenta — ele
            // pode se auto-corrigir (ex: reenviar a data no formato certo) em vez
            // de a requisicao inteira falhar por um argumento mal formado.
            return JsonSerializer.Serialize(new { error = "Argumentos invalidos. Use datas no formato YYYY-MM-DD." }, JsonOptions);
        }
    }

    private static DateOnly ReadDate(JsonElement root, string propertyName) =>
        DateOnly.Parse(root.GetProperty(propertyName).GetString()!);

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string BuildSystemPrompt(string tenantName, DateOnly today) =>
        $"""
        Voce e o Assistente do Agend.io, ajudando o dono do estabelecimento "{tenantName}" a entender os dados do proprio negocio.

        Hoje e {today:yyyy-MM-dd}. Quando o periodo perguntado nao for explicito (ex: "esse mes", "essa semana"), calcule as datas a partir de hoje.

        Regras importantes:
        - Responda SEMPRE em portugues do Brasil, de forma direta e objetiva.
        - Use as ferramentas disponiveis para buscar dados reais antes de responder qualquer pergunta numerica — nunca invente numeros.
        - Se a pergunta pedir algo fora do que as ferramentas cobrem (ex: dados de um cliente especifico), explique educadamente que voce so tem acesso a dados agregados do negocio (financeiro, agendamentos, estoque e avaliacoes).
        - Cite o periodo usado na resposta quando fizer sentido (ex: "em julho de 2026").
        - Nunca revele informacoes de outro estabelecimento — voce so tem acesso aos dados de "{tenantName}".
        """;

    private static IReadOnlyList<AiToolDefinition> BuildTools()
    {
        AiToolParameter[] periodParameters =
        [
            new AiToolParameter("from", "Data inicial (YYYY-MM-DD).", Required: true),
            new AiToolParameter("to", "Data final (YYYY-MM-DD).", Required: true),
        ];

        return
        [
            new AiToolDefinition(
                "get_cash_flow_summary",
                "Retorna faturamento recebido, pago e saldo liquido no periodo, com serie mensal e detalhamento de despesas por categoria.",
                periodParameters),
            new AiToolDefinition(
                "get_commission_report",
                "Retorna comissoes pendentes e pagas por profissional no periodo.",
                periodParameters),
            new AiToolDefinition(
                "get_appointment_stats",
                "Retorna total de agendamentos, concluidos, faltas, cancelamentos e remarcacoes no periodo, com taxas e faturamento por servico/profissional.",
                periodParameters),
            new AiToolDefinition(
                "get_reviews_summary",
                "Retorna nota media e quantidade de avaliacoes no periodo, por servico e por profissional.",
                periodParameters),
            new AiToolDefinition(
                "get_inventory_summary",
                "Retorna a foto atual do estoque: produtos ativos, produtos com estoque baixo e valor total em estoque. Sem periodo — e sempre o estado atual.",
                []),
        ];
    }
}
