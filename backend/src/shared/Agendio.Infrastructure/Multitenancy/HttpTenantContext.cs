using Agendio.SharedKernel.Multitenancy;
using Microsoft.AspNetCore.Http;

namespace Agendio.Infrastructure.Multitenancy;

/// <summary>
/// Resolve o tenant da requisicao atual a partir da claim "tenant_id" do JWT, com
/// um override manual (SetTenant) para as poucas rotas publicas que legitimamente
/// precisam operar sobre um tenant antes de existir token — ver ITenantContext.
/// Registrado como Scoped: uma instancia por requisicao HTTP.
///
/// Importante para quem for configurar Global Query Filter no DbContext de um
/// modulo: guarde a REFERENCIA a este servico num campo e chame um metodo que
/// leia HasTenant/TenantId a cada query (nunca capture o valor uma unica vez no
/// construtor do DbContext) — SetTenant pode ser chamado DEPOIS do DbContext
/// already ter sido resolvido pelo DI, e o filtro precisa refletir isso.
///
/// Resolucao de tenant hoje e SO pela claim do JWT — nao existe middleware de
/// subdominio/slug conferindo divergencia contra a rota (ver BL-06 em
/// docs/BACKLOG.md, que corrigiu a documentacao do CLAUDE.md pra refletir
/// isso). As duas camadas que seguram isolamento de verdade sao o Global
/// Query Filter do EF Core (abaixo desta classe) e a Row Level Security do
/// Postgres — confirmado por testes de isolamento cruzado, nao so por leitura
/// de codigo.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string TenantIdClaimType = "tenant_id";
    public const string TenantSlugClaimType = "tenant_slug";

    private TenantId? _explicitTenantId;
    private string? _explicitTenantSlug;

    public bool HasTenant => _explicitTenantId is not null || TryGetTenantIdClaim(out _);

    public TenantId TenantId
    {
        get
        {
            if (_explicitTenantId is not null)
            {
                return _explicitTenantId;
            }

            if (!TryGetTenantIdClaim(out var tenantId))
            {
                throw new InvalidOperationException(
                    "Nao ha tenant na requisicao atual. Verifique ITenantContext.HasTenant antes de acessar TenantId " +
                    "(rotas publicas, como o portal do cliente antes do login, nao tem tenant no token).");
            }

            return tenantId;
        }
    }

    public string? TenantSlug =>
        _explicitTenantSlug ?? httpContextAccessor.HttpContext?.User.FindFirst(TenantSlugClaimType)?.Value;

    public void SetTenant(TenantId tenantId, string? tenantSlug = null)
    {
        _explicitTenantId = tenantId;
        _explicitTenantSlug = tenantSlug;
    }

    private bool TryGetTenantIdClaim(out TenantId tenantId)
    {
        tenantId = TenantId.Empty;

        var claimValue = httpContextAccessor.HttpContext?.User.FindFirst(TenantIdClaimType)?.Value;

        if (string.IsNullOrEmpty(claimValue) || !Guid.TryParse(claimValue, out var guidValue))
        {
            return false;
        }

        tenantId = TenantId.From(guidValue);
        return true;
    }
}
