namespace Agendio.SharedKernel.Multitenancy;

/// <summary>
/// Da a Application layer de qualquer modulo acesso ao tenant da requisicao
/// atual, sem depender de Infrastructure (que resolve isso a partir do
/// HttpContext/claim do JWT). Implementacao registrada como Scoped.
/// </summary>
public interface ITenantContext
{
    bool HasTenant { get; }

    /// <summary>Lanca se HasTenant for false — use HasTenant antes em rotas publicas.</summary>
    TenantId TenantId { get; }

    string? TenantSlug { get; }

    /// <summary>
    /// Ancora manualmente o tenant da requisicao atual. Existe para as poucas
    /// rotas que legitimamente operam sobre um tenant conhecido ANTES de haver
    /// um JWT — registro de usuario e login recebem o TenantId no proprio corpo
    /// da requisicao; refresh de token descobre o TenantId ao localizar o
    /// registro pelo hash. Sem isso, o Global Query Filter e a Row Level
    /// Security bloqueariam a propria operacao que cria a sessao.
    ///
    /// Chamado automaticamente pelo ExplicitTenantBehavior para comandos que
    /// implementam IHasExplicitTenant — handlers so chamam isto manualmente
    /// quando o tenant so e conhecido DEPOIS de uma consulta (caso do refresh token).
    /// </summary>
    void SetTenant(TenantId tenantId, string? tenantSlug = null);
}
