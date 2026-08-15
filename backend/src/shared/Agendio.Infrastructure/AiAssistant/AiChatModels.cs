namespace Agendio.Infrastructure.AiAssistant;

public enum AiChatRole
{
    User,
    Assistant,
    Tool,
}

/// <summary>Argumentos sempre normalizados para uma string JSON, independente do formato nativo do provedor (objeto no Anthropic, string no OpenAI/DeepSeek).</summary>
public sealed record AiToolCall(string Id, string Name, string ArgumentsJson);

public sealed record AiChatMessage
{
    public required AiChatRole Role { get; init; }

    /// <summary>Texto do usuario ou da resposta final do assistente.</summary>
    public string? Text { get; init; }

    /// <summary>Preenchido numa mensagem Assistant que pediu execucao de ferramenta(s).</summary>
    public IReadOnlyList<AiToolCall>? ToolCalls { get; init; }

    /// <summary>Preenchido numa mensagem Tool: a qual AiToolCall.Id esta respondendo, e o resultado (texto/JSON) em Text.</summary>
    public string? ToolCallId { get; init; }
}

public sealed record AiToolParameter(string Name, string Description, bool Required, string JsonType = "string");

public sealed record AiToolDefinition(string Name, string Description, IReadOnlyList<AiToolParameter> Parameters);

public sealed record AiChatRequest(
    string SystemPrompt,
    IReadOnlyList<AiChatMessage> Messages,
    IReadOnlyList<AiToolDefinition> Tools);

public enum AiChatResultKind
{
    Text,
    ToolCalls,
}

public sealed record AiChatResult
{
    public required AiChatResultKind Kind { get; init; }

    public string? Text { get; init; }

    public IReadOnlyList<AiToolCall>? ToolCalls { get; init; }

    public static AiChatResult FromText(string text) => new() { Kind = AiChatResultKind.Text, Text = text };

    public static AiChatResult FromToolCalls(IReadOnlyList<AiToolCall> toolCalls) =>
        new() { Kind = AiChatResultKind.ToolCalls, ToolCalls = toolCalls };
}
