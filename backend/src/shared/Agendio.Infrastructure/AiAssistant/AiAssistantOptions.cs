namespace Agendio.Infrastructure.AiAssistant;

public enum AiAssistantProvider
{
    Anthropic,
    OpenAi,
    DeepSeek,
}

public sealed class AiAssistantOptions
{
    public const string SectionName = "AiAssistant";

    public required AiAssistantProvider Provider { get; init; }

    public required string ApiKey { get; init; }

    /// <summary>Nome do modelo. Vazio/null usa o default sensato de cada cliente — nomes de modelo mudam com o tempo, evitar hardcode rigido.</summary>
    public string? Model { get; init; }
}
