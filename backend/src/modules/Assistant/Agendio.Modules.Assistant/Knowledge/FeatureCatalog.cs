using Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;

namespace Agendio.Modules.Assistant.Knowledge;

/// <summary>
/// Catalogo estatico das telas reais do painel (espelha exatamente
/// frontend/components/layout/nav-config.ts -- mesmas rotas, mesmos rotulos,
/// mesmo criterio de RequiredRole via a flag `ownerOnly` de la). Se o
/// nav-config mudar (tela nova, rota renomeada), atualizar aqui tambem --
/// nao ha trava de build entre os dois projetos.
/// </summary>
public static class FeatureCatalog
{
    public const string RoleAny = "Any";
    public const string RoleOwner = "Owner";

    public static readonly IReadOnlyList<FeatureDoc> All =
    [
        new FeatureDoc(
            "painel", "Painel", "Visao geral do negocio: agendamentos de hoje, faturamento, ocupacao e insights automaticos.",
            "/painel", ["visualizar metricas do dia", "comparar com periodo anterior"], RoleAny,
            ["Appointment", "Payment"],
            ["o que tem no meu painel", "como vejo o resumo do meu negocio"]),
        new FeatureDoc(
            "agenda", "Agenda", "Calendario de agendamentos por profissional ou sala, com bloqueios e reagendamento.",
            "/agenda", ["criar agendamento", "reagendar", "cancelar com motivo", "bloquear horario"], RoleAny,
            ["Appointment", "Resource"],
            ["como marco um horario", "como remarco um agendamento", "onde vejo minha agenda"]),
        new FeatureDoc(
            "waitlist", "Lista de espera", "Clientes aguardando um horario que ainda nao esta disponivel -- avisa automaticamente se vagar.",
            "/waitlist", ["adicionar cliente na espera", "notificar quando vagar horario"], RoleAny,
            ["Appointment", "Customer"],
            ["como funciona a lista de espera", "como coloco um cliente na fila de espera"]),
        new FeatureDoc(
            "clientes", "Clientes", "Cadastro e historico de clientes, com segmentacao automatica (novo, recorrente, em risco, inativo).",
            "/clientes", ["cadastrar cliente", "ver historico", "filtrar por segmento"], RoleAny,
            ["Customer"],
            ["como cadastro um cliente", "onde vejo meus clientes", "como vejo o historico de um cliente"]),
        new FeatureDoc(
            "servicos", "Servicos", "Catalogo de servicos oferecidos, com preco e duracao.",
            "/servicos", ["cadastrar servico", "editar preco/duracao"], RoleAny,
            ["Service"],
            ["como cadastro um servico", "como mudo o preco de um servico"]),
        new FeatureDoc(
            "recursos", "Profissionais", "Cadastro de profissionais (ou salas/equipamentos), horarios de trabalho e folgas.",
            "/recursos", ["cadastrar profissional", "definir horario de trabalho", "registrar folga"], RoleAny,
            ["Resource"],
            ["como cadastro um profissional", "como cadastro um funcionario", "como registro uma folga"]),
        new FeatureDoc(
            "financeiro", "Financeiro", "Contas a pagar e a receber, comissao por profissional e fluxo de caixa.",
            "/financeiro", ["lancar conta a pagar/receber", "ver fluxo de caixa", "configurar comissao"], RoleAny,
            ["Payment", "CommissionRule"],
            ["como vejo meu financeiro", "onde lanco uma despesa", "como configuro comissao do profissional"]),
        new FeatureDoc(
            "estoque", "Estoque", "Produtos revendidos, baixa manual e alerta de estoque baixo.",
            "/estoque", ["cadastrar produto", "dar baixa em estoque", "ver produtos com estoque baixo"], RoleAny,
            ["Product"],
            ["como cadastro um produto", "onde vejo meu estoque", "como dou baixa em um produto"]),
        new FeatureDoc(
            "pagamentos", "Pagamentos", "Configuracao de cobranca de deposito/pagamento antecipado nos agendamentos.",
            "/settings/payments", ["configurar deposito obrigatorio"], RoleAny,
            ["Payment"],
            ["como configuro cobranca de deposito", "onde configuro pagamento antecipado"]),
        new FeatureDoc(
            "relatorios", "Relatorios", "Relatorios de comissao, ticket medio e desempenho por profissional/servico.",
            "/relatorios", ["ver relatorio de comissao", "ver ticket medio"], RoleAny,
            ["Appointment", "Payment"],
            ["onde vejo meus relatorios", "como vejo o relatorio de comissao"]),
        new FeatureDoc(
            "marketing", "Marketing", "Campanhas de e-mail e WhatsApp por segmento de cliente.",
            "/marketing", ["criar campanha", "segmentar publico"], RoleAny,
            ["Customer", "Campaign"],
            ["como crio uma campanha", "onde envio mensagem em massa pros clientes"]),
        new FeatureDoc(
            "whatsapp", "WhatsApp", "Conexao da conta de WhatsApp e modelos de mensagem para lembretes automaticos.",
            "/settings/whatsapp", ["conectar WhatsApp", "editar modelo de mensagem"], RoleAny,
            ["Notification"],
            ["como conecto o whatsapp", "onde configuro lembrete automatico"]),
        new FeatureDoc(
            "notificacoes", "Notificacoes", "Liga/desliga cada lembrete automatico (confirmacao, 24h antes, pos-atendimento) por e-mail e WhatsApp.",
            "/settings/notifications", ["ligar/desligar lembrete"], RoleAny,
            ["Notification"],
            ["como desligo um lembrete", "onde configuro as notificacoes"]),
        new FeatureDoc(
            "fidelidade", "Fidelidade", "Programa de pontos por atendimento e regras de resgate.",
            "/settings/loyalty", ["configurar pontos por real", "configurar resgate"], RoleAny,
            ["Customer"],
            ["como funciona o programa de fidelidade", "como configuro pontos pros clientes"]),
        new FeatureDoc(
            "unidades", "Unidades", "Cadastro de lojas/filiais do estabelecimento (endereco, cidade, estado, pais).",
            "/settings/units", ["cadastrar unidade", "vincular profissional a unidade"], RoleOwner,
            ["Unit"],
            ["como cadastro uma unidade", "como cadastro uma filial"]),
        new FeatureDoc(
            "marca", "Marca", "Personalizacao da pagina publica: logo, cores, fonte, conteudo e informacoes do estabelecimento.",
            "/settings/branding", ["trocar logo/cor", "editar conteudo da pagina publica"], RoleAny,
            ["Tenant"],
            ["como troco a cor do meu sistema", "como personalizo minha pagina publica", "onde troco o logo"]),
        new FeatureDoc(
            "equipe", "Equipe", "Convite e gestao de membros da equipe (papel Owner ou Staff).",
            "/settings/team", ["convidar membro", "ver time atual"], RoleAny,
            ["User"],
            ["como convido alguem para minha equipe", "como adiciono um funcionario ao sistema"]),
        new FeatureDoc(
            "seguranca", "Seguranca", "Troca de senha, verificacao em duas etapas (MFA) e atividade recente da conta.",
            "/settings/security", ["trocar senha", "habilitar MFA", "encerrar sessoes"], RoleAny,
            ["User"],
            ["como troco minha senha", "como ativo a verificacao em duas etapas", "onde vejo meus acessos"]),
        new FeatureDoc(
            "conta.perfil", "Minha conta", "Dados pessoais do usuario logado.",
            "/settings/account", ["editar nome/telefone"], RoleAny,
            ["User"],
            ["onde edito meu perfil", "como mudo meu nome"]),
        new FeatureDoc(
            "conta.empresa", "Dados da empresa", "Razao social, CNPJ e dados cadastrais do estabelecimento -- visivel so ao administrador.",
            "/settings/account/company", ["editar CNPJ/razao social"], RoleOwner,
            ["Tenant"],
            ["onde vejo o cnpj cadastrado", "como edito os dados da empresa"]),
        new FeatureDoc(
            "conta.plano", "Plano e assinatura", "Plano atual, fatura e cancelamento de assinatura -- visivel so ao administrador.",
            "/settings/account/plan", ["trocar de plano", "cancelar assinatura", "ver fatura"], RoleOwner,
            ["Subscription", "Payment"],
            ["como troco de plano", "como cancelo minha assinatura", "onde vejo minha fatura"]),
        new FeatureDoc(
            "ajuda", "Ajuda e informacoes", "Perguntas frequentes sobre o painel, organizadas por assunto.",
            "/settings/help", ["buscar duvida frequente"], RoleAny,
            [],
            ["onde tem ajuda", "tem alguma central de duvidas"]),
        new FeatureDoc(
            "feedback", "Feedback", "Envio de sugestao, elogio ou problema encontrado no sistema pra equipe do Agend.io.",
            "/settings/feedback", ["enviar feedback"], RoleAny,
            [],
            ["como envio um feedback", "onde reporto um problema do sistema"]),
    ];

    public static FeatureDoc? ByRoute(string route) =>
        All.FirstOrDefault(f => string.Equals(f.Route, route, StringComparison.OrdinalIgnoreCase));

    public static FeatureDoc? ById(string id) =>
        All.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Visao de um papel: Owner ve tudo, Staff nao ve os itens RoleOwner.</summary>
    public static IReadOnlyList<FeatureDoc> ForRole(string role) =>
        string.Equals(role, RoleOwner, StringComparison.OrdinalIgnoreCase)
            ? All
            : [.. All.Where(f => f.RequiredRole != RoleOwner)];

    /// <summary>
    /// Busca simples por relevancia (contagem de termos da pergunta que aparecem
    /// em nome/descricao/perguntas-exemplo) -- usada tanto pelo NavigationIntentRule
    /// (fast-path) quanto para escolher o top-N a injetar no prompt do LLM.
    /// </summary>
    public static IReadOnlyList<FeatureDoc> Search(string normalizedQuery, int maxResults = 3, int minScore = 1) =>
        Search(All, normalizedQuery, maxResults, minScore);

    /// <summary>Mesma busca, restrita a um subconjunto (ex.: FeatureCatalog.ForRole(role)) -- filtrar ANTES de pontuar, pra um item fora do papel nunca "roubar" o lugar de um item acessivel no top-N.</summary>
    public static IReadOnlyList<FeatureDoc> Search(IReadOnlyList<FeatureDoc> candidates, string normalizedQuery, int maxResults = 3, int minScore = 1)
    {
        var terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            return [];
        }

        return candidates
            .Select(feature => (feature, score: ScoreMatch(feature, terms)))
            .Where(x => x.score >= minScore)
            .OrderByDescending(x => x.score)
            .Take(maxResults)
            .Select(x => x.feature)
            .ToList();
    }

    private static int ScoreMatch(FeatureDoc feature, string[] terms)
    {
        var haystack = TextNormalization.Normalize(string.Join(' ', [feature.Name, feature.Description, .. feature.ExampleQuestions]));
        return terms.Count(term => term.Length > 2 && haystack.Contains(term, StringComparison.Ordinal));
    }
}
