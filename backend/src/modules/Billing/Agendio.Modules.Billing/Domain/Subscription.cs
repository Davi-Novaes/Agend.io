using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Billing.Domain;

/// <summary>
/// Relacao de cobranca de UM tenant com a plataforma. Deliberadamente NAO
/// implementa ITenantOwned e NAO tem Row Level Security — mesma excecao ja
/// aplicada a Tenant (Agendio.Modules.Tenancy.Domain.Tenant), e pelo mesmo
/// motivo real: tanto agendio_owner quanto agendio_app sao NOBYPASSRLS, entao
/// qualquer tabela com RLS fica invisivel cross-tenant para QUALQUER conexao —
/// inclusive o painel Super Admin e o job de conciliacao, que precisam
/// enxergar assinaturas de todos os tenants ao mesmo tempo. Leitura do proprio
/// dono filtra TenantId manualmente no código (ver GetMySubscriptionQueryHandler),
/// mesmo padrao ja usado por GetCustomerAuditLogQueryHandler.
/// </summary>
public sealed class Subscription : AggregateRoot<SubscriptionId>, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public PlanId PlanId { get; private set; } = null!;

    public SubscriptionStatus Status { get; private set; }

    public DateTimeOffset TrialEndsAtUtc { get; private set; }

    public string? AsaasCustomerId { get; private set; }

    public string? AsaasSubscriptionId { get; private set; }

    public DateTimeOffset? CurrentPeriodEndsAtUtc { get; private set; }

    public DateTimeOffset? CanceledAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private Subscription()
    {
    }

    private Subscription(TenantId tenantId, PlanId planId, DateTimeOffset trialEndsAtUtc) : base(SubscriptionId.New())
    {
        TenantId = tenantId;
        PlanId = planId;
        Status = SubscriptionStatus.Trialing;
        TrialEndsAtUtc = trialEndsAtUtc;
    }

    public static Subscription StartTrial(TenantId tenantId, PlanId planId, DateTimeOffset nowUtc) =>
        new(tenantId, planId, nowUtc.AddDays(14));

    // Sem guard de Status de proposito (alem do que o handler ja checa antes
    // de chamar isto): Cancelada NAO e mais bloqueada aqui (BL-23/BL-33,
    // docs/BACKLOG.md) — reassinar (Free ou pago) depois de cancelar e um
    // fluxo valido que o proprio app oferece na tela de billing; o guard
    // antigo rejeitava o submit sempre, mesmo com CPF/CNPJ corretos.
    public Result AttachAsaasCheckout(string asaasCustomerId, string asaasSubscriptionId)
    {
        AsaasCustomerId = asaasCustomerId;
        AsaasSubscriptionId = asaasSubscriptionId;
        return Result.Success();
    }

    public void MarkActive(DateTimeOffset currentPeriodEndsAtUtc)
    {
        Status = SubscriptionStatus.Active;
        CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc;
    }

    /// <summary>
    /// Registra a intencao de plano escolhida no onboarding — SO atualiza
    /// PlanId, nunca ativa nem toca a Asaas. Ativacao de verdade (Free direto,
    /// ou pago via checkout) so acontece depois do login, que ja exige e-mail
    /// confirmado (LoginCommandHandler) — esse e o gate real contra ativar
    /// conta/trial antes de confirmar e-mail, sem precisar nenhuma checagem
    /// cross-modulo (P1-5, docs/AUTH_BILLING_SECURITY_AUDIT.md).
    /// </summary>
    public Result SelectPlan(PlanId planId)
    {
        if (Status is SubscriptionStatus.Active)
        {
            return Result.Failure(Error.Conflict("Subscription.AlreadyActive", "Este estabelecimento ja tem uma assinatura ativa."));
        }

        PlanId = planId;
        return Result.Success();
    }

    /// <summary>
    /// Plano Free escolhido depois do login (que ja exige e-mail confirmado) —
    /// ativa direto, sem passar pela Asaas. CurrentPeriodEndsAtUtc fica null de
    /// proposito (nunca cobra, nunca vence) — BillingReconciliationJob exclui
    /// explicitamente o plano Free da varredura de trial/assinatura vencidos
    /// por esse motivo.
    /// </summary>
    public Result ActivateAsFree(PlanId freePlanId)
    {
        // Cancelada NAO e mais bloqueada aqui de proposito — mesmo raciocinio
        // de AttachAsaasCheckout (BL-23/BL-33, docs/BACKLOG.md).
        if (Status is SubscriptionStatus.Active)
        {
            return Result.Failure(Error.Conflict("Subscription.AlreadyActive", "Este estabelecimento ja tem uma assinatura ativa."));
        }

        PlanId = freePlanId;
        Status = SubscriptionStatus.Active;
        CurrentPeriodEndsAtUtc = null;
        return Result.Success();
    }

    public void MarkPastDue() => Status = SubscriptionStatus.PastDue;

    /// <summary>
    /// Pedido explicito do usuario (2026-09-05): quem ja pagou o periodo atual
    /// mantem acesso ate ele vencer, mesmo cancelando antes — so quem esta em
    /// teste gratis (Trialing, nunca pagou nada) perde acesso na hora. O
    /// corte de verdade pro caso com carencia acontece em
    /// FinalizeCancellation, chamado pelo BillingReconciliationJob quando
    /// CurrentPeriodEndsAtUtc vence.
    /// </summary>
    public Result Cancel(DateTimeOffset nowUtc)
    {
        if (Status is SubscriptionStatus.Canceled || CanceledAtUtc is not null)
        {
            return Result.Failure(Error.Validation("Subscription.AlreadyCanceled", "Esta assinatura ja foi cancelada."));
        }

        CanceledAtUtc = nowUtc;

        // Trialing (nunca pagou) ou sem periodo pago em vigor (ex.: plano
        // Free, que nunca seta CurrentPeriodEndsAtUtc) -- nada a "honrar",
        // corta na hora. Caso contrario mantem Status como esta (Active ou
        // PastDue) ate CurrentPeriodEndsAtUtc vencer -- ver FinalizeCancellation.
        var hasPaidPeriodInProgress = CurrentPeriodEndsAtUtc is { } periodEnd && periodEnd > nowUtc;
        if (Status is SubscriptionStatus.Trialing || !hasPaidPeriodInProgress)
        {
            Status = SubscriptionStatus.Canceled;
        }

        return Result.Success();
    }

    /// <summary>Fecha uma assinatura em carencia (CanceledAtUtc setado, Status ainda Active/PastDue) quando o periodo ja pago vence — chamado pelo BillingReconciliationJob.</summary>
    public void FinalizeCancellation() => Status = SubscriptionStatus.Canceled;

    /// <summary>Pagamento tardio depois do tenant ja ter sido desativado pelo job de conciliacao — reabre a assinatura.</summary>
    public void Reactivate(DateTimeOffset currentPeriodEndsAtUtc)
    {
        Status = SubscriptionStatus.Active;
        CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc;
        CanceledAtUtc = null;
    }
}
