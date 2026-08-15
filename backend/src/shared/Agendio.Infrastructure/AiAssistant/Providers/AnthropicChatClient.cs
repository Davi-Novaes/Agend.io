using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Agendio.Infrastructure.AiAssistant.Providers;

/// <summary>Implementacao via Anthropic Messages API (tool use), sem SDK oficial .NET — mesmo raciocinio de AsaasPaymentChargeClient.</summary>
public sealed class AnthropicChatClient(HttpClient httpClient, IOptions<AiAssistantOptions> options) : IAiChatClient
{
    private const string DefaultModel = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 1536;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiChatResult> SendAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["model"] = options.Value.Model is { Length: > 0 } model ? model : DefaultModel,
            ["max_tokens"] = MaxTokens,
            ["system"] = request.SystemPrompt,
            ["messages"] = BuildMessages(request),
        };

        if (request.Tools.Count > 0)
        {
            payload["tools"] = BuildTools(request.Tools);
        }

        using var response = await httpClient.PostAsJsonAsync("v1/messages", payload, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Anthropic nao retornou corpo de resposta.");

        var contentBlocks = body["content"]?.AsArray()
            ?? throw new InvalidOperationException("Resposta da Anthropic sem 'content'.");

        var toolUseBlocks = contentBlocks.Where(block => block!["type"]!.GetValue<string>() == "tool_use").ToList();
        if (toolUseBlocks.Count > 0)
        {
            var toolCalls = toolUseBlocks
                .Select(block => new AiToolCall(
                    block!["id"]!.GetValue<string>(),
                    block["name"]!.GetValue<string>(),
                    block["input"]!.ToJsonString(JsonOptions)))
                .ToList();
            return AiChatResult.FromToolCalls(toolCalls);
        }

        var text = string.Concat(contentBlocks
            .Where(block => block!["type"]!.GetValue<string>() == "text")
            .Select(block => block!["text"]!.GetValue<string>()));
        return AiChatResult.FromText(text);
    }

    // Consecutivos de Tool sao agrupados num unico turno "user" com varios blocos
    // tool_result — a API da Anthropic exige isso quando o turno anterior do
    // assistant pediu mais de uma ferramenta de uma vez.
    private static JsonArray BuildMessages(AiChatRequest request)
    {
        var messages = new JsonArray();
        var pendingToolResults = new JsonArray();

        void FlushToolResults()
        {
            if (pendingToolResults.Count > 0)
            {
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = pendingToolResults });
                pendingToolResults = [];
            }
        }

        foreach (var message in request.Messages)
        {
            if (message.Role == AiChatRole.Tool)
            {
                pendingToolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = message.ToolCallId,
                    ["content"] = message.Text,
                });
                continue;
            }

            FlushToolResults();

            messages.Add(message.Role switch
            {
                AiChatRole.User => new JsonObject { ["role"] = "user", ["content"] = message.Text },
                AiChatRole.Assistant when message.ToolCalls is { Count: > 0 } => new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = new JsonArray(message.ToolCalls.Select(call => (JsonNode)new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = call.Id,
                        ["name"] = call.Name,
                        ["input"] = JsonNode.Parse(call.ArgumentsJson),
                    }).ToArray()),
                },
                AiChatRole.Assistant => new JsonObject { ["role"] = "assistant", ["content"] = message.Text },
                _ => throw new InvalidOperationException($"AiChatRole nao suportado: {message.Role}"),
            });
        }

        FlushToolResults();
        return messages;
    }

    private static JsonArray BuildTools(IReadOnlyList<AiToolDefinition> tools) =>
        new(tools.Select(tool => (JsonNode)new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(tool.Parameters.Select(p =>
                    new KeyValuePair<string, JsonNode?>(p.Name, new JsonObject { ["type"] = p.JsonType, ["description"] = p.Description }))),
                ["required"] = new JsonArray(tool.Parameters.Where(p => p.Required).Select(p => (JsonNode)p.Name).ToArray()),
            },
        }).ToArray());

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Anthropic retornou {(int)response.StatusCode}: {body}");
        }
    }
}
