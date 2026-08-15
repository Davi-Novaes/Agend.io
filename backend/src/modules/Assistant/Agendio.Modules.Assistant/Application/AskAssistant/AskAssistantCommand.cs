using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Assistant.Application.AskAssistant;

// Role: "user" | "assistant" — historico enviado pelo frontend, mantido so em
// memoria no cliente (Fase 22: sem persistencia server-side de conversas, ver
// escopo). Comando (nao Query) porque tem efeito colateral real: chama a API
// externa de IA, com custo por chamada.
public sealed record AssistantChatMessageDto(string Role, string Text);

public sealed record AskAssistantCommand(string Question, IReadOnlyList<AssistantChatMessageDto> History) : ICommand<AskAssistantResult>;

public sealed record AskAssistantResult(string Answer);
