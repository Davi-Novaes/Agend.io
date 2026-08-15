using Microsoft.Extensions.Options;

namespace Agendio.Infrastructure.AiAssistant.Providers;

public sealed class OpenAiChatClient(HttpClient httpClient, IOptions<AiAssistantOptions> options)
    : OpenAiCompatibleChatClient(httpClient, options, defaultModel: "gpt-4o-mini");
