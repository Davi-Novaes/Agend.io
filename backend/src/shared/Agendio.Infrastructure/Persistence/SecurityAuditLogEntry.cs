namespace Agendio.Infrastructure.Persistence;

/// <summary>
/// Trilha de auditoria de SEGURANCA — distinta de AuditLogEntry (que audita
/// mudanca de dado de negocio). Aqui vivem eventos de autenticacao: login
/// sucesso/falha, bloqueio de conta, MFA habilitado/desabilitado, senha trocada/
/// recuperada, e-mail confirmado. Append-only, nunca alterada por codigo de
/// aplicacao.
///
/// Mesmo padrao de tabela-por-modulo de AuditLogEntry: cada modulo que
/// autentica identidade (Identity, Platform) declara o PROPRIO DbSet e chama
/// ConfigureSecurityAuditLog no seu OnModelCreating — nao entra em
/// AgendioDbContextBase porque a maioria dos modulos nunca autentica ninguem
/// (Scheduling, Billing, Estoque...) e nao precisaria da tabela.
///
/// Metadata e um JSON curto e deliberadamente pobre: nunca senha, token bruto,
/// segredo TOTP, numero de cartao ou CVV (regra inegociavel do CLAUDE.md) — so
/// contexto de negocio (ex.: motivo do bloqueio, EventType da Asaas).
/// </summary>
public sealed class SecurityAuditLogEntry
{
    public Guid Id { get; private init; }

    /// <summary>Null para eventos anteriores a resolucao do tenant (ex.: login com e-mail inexistente).</summary>
    public Guid? TenantId { get; private init; }

    /// <summary>UserId ou PlatformAdminId do ator, quando conhecido (null em falha antes do usuario ser identificado).</summary>
    public Guid? ActorId { get; private init; }

    public string EventType { get; private init; } = string.Empty;

    public bool Success { get; private init; }

    public string? IpAddress { get; private init; }

    /// <summary>Codigo de pais ISO 3166-1 alpha-2 (ex.: "BR", "US") — vem do header "CF-IPCountry" da borda da Cloudflare, nunca de um servico de geolocalizacao externo (sem custo, sem chamada extra, sem vazar o IP pra terceiro).</summary>
    public string? CountryCode { get; private init; }

    /// <summary>Regiao/estado (ex.: "Sao Paulo") — header "CF-Region" da Cloudflare. So populado se a zona tiver "Add visitor location headers" habilitado (Managed Transforms) — null nao e erro, e ausencia de configuracao.</summary>
    public string? Region { get; private init; }

    /// <summary>Cidade (ex.: "Sao Paulo") — header "CF-IPCity" da Cloudflare, mesma condicao de Region.</summary>
    public string? City { get; private init; }

    public string? UserAgent { get; private init; }

    public string? Metadata { get; private init; }

    public DateTimeOffset OccurredAtUtc { get; private init; }

    private SecurityAuditLogEntry()
    {
    }

    public static SecurityAuditLogEntry Create(
        Guid? tenantId, Guid? actorId, string eventType, bool success, string? ipAddress, string? countryCode,
        string? region, string? city, string? userAgent, string? metadata, DateTimeOffset occurredAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorId = actorId,
            EventType = eventType,
            Success = success,
            IpAddress = ipAddress,
            CountryCode = countryCode,
            Region = region,
            City = city,
            UserAgent = userAgent,
            Metadata = metadata,
            OccurredAtUtc = occurredAtUtc,
        };
}
