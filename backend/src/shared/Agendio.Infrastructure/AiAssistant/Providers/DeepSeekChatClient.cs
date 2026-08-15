using Microsoft.Extensions.Options;

namespace Agendio.Infrastructure.AiAssistant.Providers;

/// <summary>DeepSeek documenta a propria API como compativel com o formato de function calling da OpenAI — so BaseUrl e modelo mudam.</summary>
public sealed class DeepSeekChatClient(HttpClient httpClient, IOptions<AiAssistantOptions> options)
    : OpenAiCompatibleChatClient(httpClient, options, defaultModel: "deepseek-chat");
