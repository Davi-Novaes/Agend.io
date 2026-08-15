using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Agendio.Infrastructure.AiAssistant.Providers;

/// <summary>
/// Base compartilhada para provedores compativeis com o formato de function
/// calling da OpenAI (Chat Completions API) — a DeepSeek documenta a propria
/// API como compativel com esse mesmo formato, so muda BaseUrl e modelo default.
/// Evita duplicar a traducao de AiChatRequest/AiChatResult duas vezes.
/// </summary>
public abstract class OpenAiCompatibleChatClient(HttpClient httpClient, IOptions<AiAssistantOptions> options, string defaultModel) : IAiChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiChatResult> SendAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["model"] = options.Value.Model is { Length: > 0 } model ? model : defaultModel,
            ["messages"] = BuildMessages(request),
        };

        if (request.Tools.Count > 0)
        {
            payload["tools"] = BuildTools(request.Tools);
        }

        using var response = await httpClient.PostAsJsonAsync("chat/completions", payload, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Provedor de IA nao retornou corpo de resposta.");

        var message = body["choices"]?[0]?["message"]?.AsObject()
            ?? throw new InvalidOperationException("Resposta do provedor de IA sem 'choices[0].message'.");

        var toolCallsNode = message["tool_calls"]?.AsArray();
        if (toolCallsNode is { Count: > 0 })
        {
            var toolCalls = toolCallsNode
                .Select(node => new AiToolCall(
                    node!["id"]!.GetValue<string>(),
                    node["function"]!["name"]!.GetValue<string>(),
                    node["function"]!["arguments"]!.GetValue<string>()))
                .ToList();
            return AiChatResult.FromToolCalls(toolCalls);
        }

        var text = message["content"]?.GetValue<string>() ?? string.Empty;
        return AiChatResult.FromText(text);
    }

    private static JsonArray BuildMessages(AiChatRequest request)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
        };

        foreach (var message in request.Messages)
        {
            messages.Add(message.Role switch
            {
                AiChatRole.User => new JsonObject { ["role"] = "user", ["content"] = message.Text },
                AiChatRole.Assistant when message.ToolCalls is { Count: > 0 } => new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = null,
                    ["tool_calls"] = new JsonArray(message.ToolCalls.Select(call => (JsonNode)new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.ArgumentsJson },
                    }).ToArray()),
                },
                AiChatRole.Assistant => new JsonObject { ["role"] = "assistant", ["content"] = message.Text },
                AiChatRole.Tool => new JsonObject { ["role"] = "tool", ["tool_call_id"] = message.ToolCallId, ["content"] = message.Text },
                _ => throw new InvalidOperationException($"AiChatRole nao suportado: {message.Role}"),
            });
        }

        return messages;
    }

    private static JsonArray BuildTools(IReadOnlyList<AiToolDefinition> tools) =>
        new(tools.Select(tool => (JsonNode)new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject(tool.Parameters.Select(p =>
                        new KeyValuePair<string, JsonNode?>(p.Name, new JsonObject { ["type"] = p.JsonType, ["description"] = p.Description }))),
                    ["required"] = new JsonArray(tool.Parameters.Where(p => p.Required).Select(p => (JsonNode)p.Name).ToArray()),
                },
            },
        }).ToArray());

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Provedor de IA retornou {(int)response.StatusCode}: {body}");
        }
    }
}
