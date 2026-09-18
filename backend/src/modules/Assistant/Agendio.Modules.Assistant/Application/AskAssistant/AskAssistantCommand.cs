using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Assistant.Application.AskAssistant;

// Role: "user" | "assistant" — historico enviado pelo frontend, mantido so em
// memoria no cliente (Fase 22: sem persistencia server-side de conversas, ver
// escopo). Comando (nao Query) porque tem efeito colateral real: chama a API
// externa de IA, com custo por chamada.
public sealed record AssistantChatMessageDto(string Role, string Text);

// CallerRole ("Owner"/"Staff") vem da claim do JWT, resolvida no endpoint --
// usada so pelo fast-path (NavigationIntentRule) pra nao sugerir uma tela que
// o usuario nem consegue abrir. O caminho LLM ja recusa educadamente pedidos
// fora do escopo, entao nao precisa da role hoje. CallerUserId vem da mesma
// claim (NameIdentifier) -- so pro log de auditoria (Fase 3), nunca usado
// pra logica de negocio.
public sealed record AskAssistantCommand(string Question, IReadOnlyList<AssistantChatMessageDto> History, string CallerRole, Guid CallerUserId) : ICommand<AskAssistantResult>;

/// <summary>SuggestedRoute preenchido so quando a resposta veio (ou se refere a) uma tela especifica -- o frontend usa pra mostrar um botao "Ir para X".</summary>
public sealed record AskAssistantResult(string Answer, string? SuggestedRoute = null);
