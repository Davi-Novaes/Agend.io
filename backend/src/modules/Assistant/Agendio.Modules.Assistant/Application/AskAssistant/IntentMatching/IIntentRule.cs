namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;

/// <summary>
/// Resultado de uma regra que reconheceu a pergunta -- IntentId vira o valor
/// gravado em auditoria (Fase 3) e ExtractedArgs carrega o que a regra
/// conseguiu extrair (ex.: periodo) para quem for buscar o dado.
/// </summary>
public sealed record IntentMatch(string IntentId, IReadOnlyDictionary<string, string> ExtractedArgs);

/// <summary>SuggestedRoute preenchido so quando faz sentido oferecer um botao "Ir para X" junto da resposta.</summary>
public sealed record IntentAnswer(string Text, string? SuggestedRoute = null);

/// <summary>
/// Uma regra do fast-path: tenta reconhecer, com ALTA confianca, uma pergunta
/// comum e bem definida, sem chamar IA nenhuma. Precisa ser conservadora --
/// ambiguidade deve sempre retornar false (nunca "quase certo"), pra cair no
/// loop de tool-calling com LLM em vez de arriscar responder errado. Registrar
/// uma implementacao nova via DI (AddAssistantModule) e o unico passo pra
/// adicionar um intent -- nao precisa tocar em AskAssistantCommandHandler.
/// </summary>
public interface IIntentRule
{
    string IntentId { get; }

    bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match);

    /// <summary>Monta a resposta em texto fixo (nao geracao livre) -- so chamado depois de um TryMatch verdadeiro, com o MESMO match retornado.</summary>
    Task<IntentAnswer> ResolveAsync(IntentMatch match, CancellationToken cancellationToken);
}
