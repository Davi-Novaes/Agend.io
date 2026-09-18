using System.Globalization;
using System.Text.Json;
using Agendio.Infrastructure.AiAssistant;
using Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;
using Agendio.Modules.Assistant.Infrastructure;
using Agendio.Modules.Assistant.Knowledge;
using Agendio.Modules.Customers.Contracts;
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
    IntentMatcher intentMatcher,
    IAiChatClient chatClient,
    IOptions<AiAssistantOptions> aiOptions,
    IFinanceSummaryLookupService financeLookup,
    IAppointmentStatsLookupService appointmentStatsLookup,
    IReviewsSummaryLookupService reviewsLookup,
    IInventorySummaryLookupService inventoryLookup,
    ICustomerDirectoryLookupService customerDirectoryLookup,
    ICustomerLookupService customerLookup,
    ITenantLookupService tenantLookup,
    ITenantContext tenantContext,
    IAssistantQueryLogger queryLogger,
    IClock clock) : ICommandHandler<AskAssistantCommand, AskAssistantResult>
{
    // Limite de idas-e-voltas de ferramenta por pergunta — protege contra um
    // loop indefinido (custo por chamada de IA) sem cortar perguntas legitimas
    // que precisam de 2-3 ferramentas (ex: "faturamento e estoque baixo").
    private const int MaxToolIterations = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<AskAssistantResult>> Handle(AskAssistantCommand request, CancellationToken cancellationToken)
    {
        // Fast-path primeiro, SEMPRE -- so entra no loop de tool-calling (custo de
        // IA) se nenhuma regra reconhecer a pergunta com alta confianca. Nao
        // depende de aiOptions/tenant lookup abaixo, entao funciona mesmo se o
        // assistente de IA nao estiver configurado nesta instalacao.
        var fastPathResult = await intentMatcher.TryResolveAsync(request.Question, request.CallerRole, cancellationToken);
        if (fastPathResult.Matched)
        {
            await LogAsync(request, resolvedIntent: fastPathResult.IntentId, source: "FastPath", toolOrServiceUsed: null, success: true, errorCode: null, cancellationToken);
            return Result.Success(new AskAssistantResult(fastPathResult.Answer!, fastPathResult.SuggestedRoute));
        }

        // Sem IA configurada: nao e um erro (a arquitetura foi pensada pra
        // funcionar so com fast-path, ver IIntentRule) -- resposta graciosa em
        // vez de inventar uma resposta ou devolver uma falha de HTTP pro chat.
        // "Nao invento" (regra do produto): melhor admitir que nao sabe do que
        // arriscar um numero errado.
        if (string.IsNullOrEmpty(aiOptions.Value.ApiKey))
        {
            await LogAsync(request, resolvedIntent: null, source: "NoMatch", toolOrServiceUsed: null, success: true, errorCode: null, cancellationToken);
            const string noMatchAnswer =
                "Ainda nao sei responder essa pergunta. Voce pode me perguntar sobre agenda, clientes, faturamento, despesas, estoque ou como usar alguma tela do sistema.";
            return Result.Success(new AskAssistantResult(noMatchAnswer, "/settings/help"));
        }

        var tenant = await tenantLookup.FindByIdAsync(tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            const string errorCode = "Assistant.TenantNotFound";
            await LogAsync(request, resolvedIntent: null, source: "Llm", toolOrServiceUsed: null, success: false, errorCode, cancellationToken);
            return Result.Failure<AskAssistantResult>(Error.Failure(errorCode, "Estabelecimento nao encontrado."));
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, ResolveTimeZone(tenant.TimeZoneId)).DateTime);
        var relevantFeatures = FeatureCatalog.Search(FeatureCatalog.ForRole(request.CallerRole), TextNormalization.Normalize(request.Question));
        var systemPrompt = BuildSystemPrompt(tenant.Name, today, relevantFeatures);
        var tools = BuildTools();

        var messages = request.History
            .Select(m => new AiChatMessage { Role = m.Role == "assistant" ? AiChatRole.Assistant : AiChatRole.User, Text = m.Text })
            .ToList();
        messages.Add(new AiChatMessage { Role = AiChatRole.User, Text = request.Question });

        var toolsUsed = new List<string>();
        string? ToolsUsedOrNull() => toolsUsed.Count > 0 ? string.Join(",", toolsUsed) : null;

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            AiChatResult response;
            try
            {
                response = await chatClient.SendAsync(new AiChatRequest(systemPrompt, messages, tools), cancellationToken);
            }
            catch (Exception)
            {
                const string errorCode = "Assistant.Unavailable";
                await LogAsync(request, resolvedIntent: null, source: "Llm", ToolsUsedOrNull(), success: false, errorCode, cancellationToken);
                return Result.Failure<AskAssistantResult>(Error.Failure(
                    errorCode, "Nao foi possivel falar com o assistente agora. Tente novamente em instantes."));
            }

            if (response.Kind == AiChatResultKind.Text)
            {
                await LogAsync(request, resolvedIntent: null, source: "Llm", ToolsUsedOrNull(), success: true, errorCode: null, cancellationToken);
                return Result.Success(new AskAssistantResult(response.Text ?? string.Empty));
            }

            var toolCalls = response.ToolCalls!;
            messages.Add(new AiChatMessage { Role = AiChatRole.Assistant, ToolCalls = toolCalls });

            foreach (var toolCall in toolCalls)
            {
                toolsUsed.Add(toolCall.Name);
                var toolResultJson = await ExecuteToolAsync(toolCall, cancellationToken);
                messages.Add(new AiChatMessage { Role = AiChatRole.Tool, ToolCallId = toolCall.Id, Text = toolResultJson });
            }
        }

        const string tooManyStepsErrorCode = "Assistant.TooManySteps";
        await LogAsync(request, resolvedIntent: null, source: "Llm", ToolsUsedOrNull(), success: false, tooManyStepsErrorCode, cancellationToken);
        return Result.Failure<AskAssistantResult>(Error.Failure(
            tooManyStepsErrorCode, "Nao consegui montar uma resposta completa para essa pergunta. Tente reformular de um jeito mais simples."));
    }

    private Task LogAsync(
        AskAssistantCommand request, string? resolvedIntent, string source, string? toolOrServiceUsed, bool success, string? errorCode, CancellationToken cancellationToken) =>
        queryLogger.LogAsync(request.CallerUserId, request.Question, resolvedIntent, source, toolOrServiceUsed, success, errorCode, cancellationToken);

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
                "get_low_stock_products" => await inventoryLookup.ListLowStockAsync(cancellationToken),
                "get_customer_count" => await customerDirectoryLookup.GetSummaryAsync(cancellationToken),
                "get_inactive_customers" => await customerLookup.ListInactiveSinceAsync(ReadOptionalInt(root, "days", 60), cancellationToken),
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
        DateOnly.Parse(root.GetProperty(propertyName).GetString()!, CultureInfo.InvariantCulture);

    private static int ReadOptionalInt(JsonElement root, string propertyName, int defaultValue) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : defaultValue;

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

    private static string BuildSystemPrompt(string tenantName, DateOnly today, IReadOnlyList<FeatureDoc> relevantFeatures)
    {
        var knowledgeSection = relevantFeatures.Count == 0
            ? ""
            : $"""

              Telas do sistema relacionadas a esta pergunta (use pra explicar como usar o sistema, sempre citando o nome exato da tela):
              {string.Join("\n", relevantFeatures.Select(f => $"- \"{f.Name}\": {f.Description} Capacidades: {string.Join(", ", f.Capabilities)}."))}
              """;

        return $"""
        Voce e o Assistente do Agend.io, ajudando o dono do estabelecimento "{tenantName}" a entender os dados do proprio negocio e a usar o sistema.

        Hoje e {today:yyyy-MM-dd}. Quando o periodo perguntado nao for explicito (ex: "esse mes", "essa semana"), calcule as datas a partir de hoje.

        Regras importantes:
        - Responda SEMPRE em portugues do Brasil, de forma direta e objetiva.
        - Use as ferramentas disponiveis para buscar dados reais antes de responder qualquer pergunta numerica — nunca invente numeros.
        - Se a pergunta for sobre como usar uma funcionalidade do sistema, use as telas listadas abaixo (se houver) -- nunca invente uma tela, rota ou funcionalidade que nao esteja listada.
        - Se a pergunta pedir algo fora do que as ferramentas E as telas listadas cobrem (ex: telefone/e-mail/historico de UM cliente especifico pelo nome), explique educadamente que voce so tem acesso a dados agregados do negocio (financeiro, agendamentos, estoque, avaliacoes e contagem/lista de clientes) e as telas do sistema — nunca ao cadastro individual de um cliente.
        - Cite o periodo usado na resposta quando fizer sentido (ex: "em julho de 2026").
        - Nunca revele informacoes de outro estabelecimento — voce so tem acesso aos dados de "{tenantName}".
        {knowledgeSection}
        """;
    }

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
                "Retorna total de agendamentos, concluidos, faltas, cancelamentos e remarcacoes no periodo, com taxas, faturamento por servico/profissional E ranking de profissional por QUANTIDADE de atendimentos concluidos (use para perguntas de 'qual profissional mais atendeu'/'ranking por volume', distinto do ranking por receita).",
                periodParameters),
            new AiToolDefinition(
                "get_reviews_summary",
                "Retorna nota media e quantidade de avaliacoes no periodo, por servico e por profissional.",
                periodParameters),
            new AiToolDefinition(
                "get_inventory_summary",
                "Retorna a foto atual do estoque: produtos ativos, produtos com estoque baixo e valor total em estoque. Sem periodo — e sempre o estado atual.",
                []),
            new AiToolDefinition(
                "get_low_stock_products",
                "Retorna a LISTA (nome e quantidade) dos produtos ativos com estoque no minimo ou abaixo. Sem periodo — e sempre o estado atual.",
                []),
            new AiToolDefinition(
                "get_customer_count",
                "Retorna a contagem de clientes ativos e inativos cadastrados. Nunca retorna dados de um cliente especifico.",
                []),
            new AiToolDefinition(
                "get_inactive_customers",
                "Retorna clientes ativos sem contato ha pelo menos N dias (nome, sem telefone/e-mail). Use 'days' se a pergunta especificar um numero de dias, senao omita (padrao 60).",
                [new AiToolParameter("days", "Corte em dias sem contato (opcional, padrao 60).", Required: false, JsonType: "integer")]),
        ];
    }
}
