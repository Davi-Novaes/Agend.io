using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Platform.Infrastructure.Jobs;

/// <summary>
/// Roda a cada 15 minutos (ver registro em Program.cs) — mesma janela do
/// intervalo do job, entao cada execucao so olha pro que aconteceu desde a
/// anterior (sem estado extra pra rastrear "ja alertei sobre isto"). Se o
/// padrao continuar no proximo ciclo, alerta de novo — preferivel a perder um
/// ataque em andamento por deduplicacao excessivamente esperta.
///
/// Duas heuristicas deliberadamente simples (nao um sistema de deteccao de
/// fraude): muitas falhas de login do MESMO IP, ou muitos cadastros novos do
/// MESMO IP, na janela — ambas fortes indicios de automacao (forca bruta ou
/// spam de conta), nunca comportamento humano normal.
/// </summary>
public sealed class SecurityAlertJob(
    PlatformDbContext dbContext,
    ISecurityAuditReader identitySecurityAuditReader,
    IEmailSender emailSender,
    IClock clock,
    ILogger<SecurityAlertJob> logger)
{
    private const int WindowMinutes = 15;
    private const int FailedLoginThreshold = 8;
    private const int NewAccountThreshold = 5;

    private static readonly HashSet<string> FailureEventTypes = ["LoginFailed", "AccountLocked", "LoginBlocked"];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var sinceUtc = clock.UtcNow.AddMinutes(-WindowMinutes);

        var identityEvents = await identitySecurityAuditReader.GetRecentEventsAsync(sinceUtc, cancellationToken);
        var platformEvents = await dbContext.SecurityAuditLog
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= sinceUtc)
            .ToListAsync(cancellationToken);

        var allEvents = identityEvents
            .Select(e => (e.EventType, e.Success, e.IpAddress))
            .Concat(platformEvents.Select(e => (e.EventType, e.Success, e.IpAddress)));

        var findings = new List<string>();

        var suspiciousLoginIps = allEvents
            .Where(e => !e.Success && FailureEventTypes.Contains(e.EventType) && e.IpAddress is not null)
            .GroupBy(e => e.IpAddress)
            .Where(g => g.Count() >= FailedLoginThreshold)
            .Select(g => (Ip: g.Key!, Count: g.Count()));

        foreach (var (ip, count) in suspiciousLoginIps)
        {
            findings.Add($"{count} tentativas de login falhas do IP {ip} nos ultimos {WindowMinutes} minutos.");
        }

        var suspiciousSignupIps = identityEvents
            .Where(e => e.EventType == "RegistrationConfirmed" && e.IpAddress is not null)
            .GroupBy(e => e.IpAddress)
            .Where(g => g.Count() >= NewAccountThreshold)
            .Select(g => (Ip: g.Key!, Count: g.Count()));

        foreach (var (ip, count) in suspiciousSignupIps)
        {
            findings.Add($"{count} contas novas criadas do IP {ip} nos ultimos {WindowMinutes} minutos.");
        }

        if (findings.Count == 0)
        {
            return;
        }

        logger.LogWarning("Atividade suspeita detectada: {Findings}", string.Join(" | ", findings));

        var recipients = await dbContext.PlatformAdmins
            .Where(a => a.IsActive)
            .Select(a => a.Email)
            .ToListAsync(cancellationToken);

        var htmlBody = $"""
            <p>Atividade suspeita detectada no Agendio nos ultimos {WindowMinutes} minutos:</p>
            <ul>{string.Join("", findings.Select(f => $"<li>{f}</li>"))}</ul>
            <p>Veja o log completo no painel do Super Admin, em Seguranca &gt; Atividade.</p>
            """;

        foreach (var recipient in recipients)
        {
            await emailSender.SendAsync(recipient, "[Agendio] Atividade suspeita detectada", htmlBody, cancellationToken);
        }
    }
}
