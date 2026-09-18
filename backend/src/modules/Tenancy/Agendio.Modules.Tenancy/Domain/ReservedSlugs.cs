namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Slugs que nenhum tenant pode reivindicar porque colidiriam com um
/// subdominio da propria plataforma (ver ADR 0009 — pagina publica agora
/// resolve por barbearia-do-ze.agendiobr.com.br, nao so por path). Mantida em
/// sincronia manual com RESERVED_SUBDOMAINS em frontend/proxy.ts — e quem
/// realmente bloqueia o cadastro (CreateTenantCommandHandler); o proxy so
/// evita reescrever a URL de um subdominio que nunca poderia ser um slug.
/// </summary>
public static class ReservedSlugs
{
    private static readonly HashSet<string> Values = new(StringComparer.Ordinal)
    {
        "www", "api", "app", "admin", "painel", "platform", "superadmin", "root",
        "mail", "email", "smtp", "send", "ftp", "ns1", "ns2", "cdn", "static", "assets",
        "blog", "docs", "status", "support", "help", "dashboard",
        "test", "staging", "dev", "localhost",
    };

    public static bool IsReserved(string slug) => Values.Contains(slug);
}
