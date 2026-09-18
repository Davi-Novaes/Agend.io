namespace Agendio.Modules.Assistant.Knowledge;

/// <summary>
/// Uma entrada do catalogo estatico de telas/funcionalidades do Agend.io --
/// fonte unica consumida por dois lugares: o endpoint GET /api/assistant/features
/// (alimenta as sugestoes e o botao "Ir para X" do frontend) e o proprio
/// assistente (NavigationIntentRule no fast-path, e o system prompt no
/// fallback de IA, que recebe so o top-N relevante pra nao estourar contexto).
///
/// E dado ESTATICO em codigo (nao seed em banco/CMS) de proposito -- a fonte
/// da verdade de uma tela e o proprio codigo do frontend (rota real do App
/// Router), e manter isso sincronizado e responsabilidade de quem mexe na
/// tela (mesmo espirito de qualquer doc-comment que descreve codigo: fica
/// desatualizado se ninguem atualizar, sem trava de build possivel entre
/// backend e frontend no mesmo monorepo).
/// </summary>
public sealed record FeatureDoc(
    string Id,
    string Name,
    string Description,
    string Route,
    IReadOnlyList<string> Capabilities,
    string RequiredRole,
    IReadOnlyList<string> RelatedEntities,
    IReadOnlyList<string> ExampleQuestions);
