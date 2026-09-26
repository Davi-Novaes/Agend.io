using Agendio.Infrastructure.Persistence;
using Agendio.SharedKernel.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Agendio.Infrastructure.Security;

/// <summary>
/// Implementacao generica por DbContext de modulo — cada modulo (Identity,
/// Platform) registra `AddScoped&lt;SecurityAuditLogger&lt;SeuDbContext&gt;&gt;()` no
/// proprio DI e injeta esse tipo FECHADO diretamente (nunca uma interface
/// `ISecurityAuditLogger` compartilhada — dois modulos registrando a MESMA
/// interface faziam o ultimo registro "vencer" silenciosamente para qualquer
/// consumidor, inclusive o do outro modulo, escrevendo no schema errado sem
/// erro nenhum). Escreve na tabela security_audit_log DAQUELE schema (nunca
/// compartilhada entre modulos — mesmo raciocinio de AuditLogEntry).
///
/// SaveChanges roda imediatamente, isolado de qualquer alteracao ainda
/// pendente no mesmo DbContext: um evento de seguranca precisa ficar gravado
/// mesmo que o resto da operacao falhe ou seja revertido depois (ex.: log de
/// tentativa de login falha nao pode desaparecer se o handler decidir nao
/// commitar mais nada).
/// </summary>
public sealed class SecurityAuditLogger<TContext>(TContext dbContext, IClock clock, IHttpContextAccessor httpContextAccessor)
    where TContext : DbContext
{
    public async Task LogAsync(
        string eventType, bool success, Guid? tenantId, Guid? actorId, string? metadata, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        var entry = SecurityAuditLogEntry.Create(
            tenantId,
            actorId,
            eventType,
            success,
            ResolveClientIp(httpContext),
            ResolveCountryCode(httpContext),
            ResolveHeaderValue(httpContext, "CF-Region", 100),
            ResolveHeaderValue(httpContext, "CF-IPCity", 100),
            Truncate(httpContext?.Request.Headers.UserAgent.ToString(), 512),
            metadata,
            clock.UtcNow);

        dbContext.Set<SecurityAuditLogEntry>().Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Em producao a cadeia real e navegador -&gt; borda da Cloudflare -&gt; Cloudflare
    /// Tunnel -&gt; Caddy (localhost) -&gt; Kestrel: "X-Forwarded-For" chega com DOIS
    /// saltos (IP real + o loopback do Caddy), mas o ForwardedHeadersMiddleware
    /// em Program.cs usa ForwardLimit padrao (1), entao RemoteIpAddress acaba
    /// resolvendo pro salto mais proximo (127.0.0.1), nunca o visitante real.
    /// "CF-Connecting-Ip" e mais confiavel: a propria borda da Cloudflare
    /// sobrescreve esse header com o IP real do visitante e — como o Caddy so
    /// escuta em localhost, so alcancavel pelo tunnel — nao ha como um
    /// requisitante externo forjar esse valor antes de chegar aqui.
    /// </summary>
    private static string? ResolveClientIp(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var cfConnectingIp = httpContext.Request.Headers["CF-Connecting-IP"].ToString().Trim();
        var candidate = string.IsNullOrWhiteSpace(cfConnectingIp)
            ? httpContext.Connection.RemoteIpAddress
            : IPAddress.TryParse(cfConnectingIp, out var parsed) ? parsed : null;

        return candidate?.IsIPv4MappedToIPv6 == true
            ? candidate.MapToIPv4().ToString()
            : candidate?.ToString();
    }

    /// <summary>
    /// "CF-IPCountry" tambem vem pronto da borda da Cloudflare (ISO 3166-1
    /// alpha-2, ou "XX"/"T1" em casos especiais — ver docs da Cloudflare) —
    /// mesma confiabilidade de CF-Connecting-Ip, e evita qualquer chamada a um
    /// servico de geolocalizacao de terceiro (custo, latencia, e o IP do
    /// usuario vazando pra mais um lugar).
    /// </summary>
    private static string? ResolveCountryCode(HttpContext? httpContext)
    {
        var countryCode = httpContext?.Request.Headers["CF-IPCountry"].ToString();
        return string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2 ? null : countryCode.ToUpperInvariant();
    }

    /// <summary>
    /// "CF-Region" (estado) e "CF-IPCity" (cidade) so chegam se a zona da
    /// Cloudflare tiver o Managed Transform "Add visitor location headers"
    /// habilitado (Network settings do dashboard, fora do alcance do backend) —
    /// por isso null aqui e ausencia de configuracao, nao bug de leitura.
    /// </summary>
    private static string? ResolveHeaderValue(HttpContext? httpContext, string headerName, int maxLength)
    {
        var value = httpContext?.Request.Headers[headerName].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            value = Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            // O header ainda e util sem decodificacao; nunca deixe telemetria quebrar o login.
        }

        return Truncate(value, maxLength);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
