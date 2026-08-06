using Hangfire.Dashboard;

namespace Agendio.Api;

/// <summary>
/// Sprint 0: restringe o dashboard do Hangfire a requisicoes locais, so para dar
/// visibilidade durante o desenvolvimento. Autorizacao real (Super Admin
/// autenticado com escopo "platform") entra no Sprint 6 — ver roadmap em
/// docs/adr. NUNCA usar isto como esta em producao.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.Connection.RemoteIpAddress is null || System.Net.IPAddress.IsLoopback(httpContext.Connection.RemoteIpAddress);
    }
}
