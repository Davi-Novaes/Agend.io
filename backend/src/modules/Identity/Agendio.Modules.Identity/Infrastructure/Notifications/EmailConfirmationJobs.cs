using Agendio.Infrastructure;
using Agendio.Infrastructure.Notifications;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Identity.Infrastructure.Notifications;

/// <summary>
/// Job do Hangfire (mesmo molde de AppointmentNotificationJobs) — a durabilidade
/// e o retry automatico em falha transitoria (SMTP fora do ar) sao do proprio
/// Hangfire, sem logica extra aqui. E o UNICO jeito do dono entrar na propria
/// conta apos o cadastro, por isso via Hangfire (com retry) e nao sincrono/
/// best-effort como o convite de equipe.
///
/// O token e gerado AQUI DENTRO (nao recebido como parametro do job) de proposito:
/// argumentos de job Hangfire ficam persistidos em texto plano na tabela do
/// Postgres ate o job concluir — um segredo de posse de conta nao deveria viver
/// la. So o hash (ja protegido pelo mesmo SHA-256 de TeamInvitation/RefreshToken)
/// e persistido em User. Mesmo metodo serve para o reenvio (gera um token novo,
/// substitui o anterior).
///
/// Roda fora de uma requisicao HTTP (sem JWT, sem tenant ambiente) — por isso
/// ancora o ITenantContext manualmente, o mesmo papel que ExplicitTenantBehavior
/// cumpre para comandos publicos.
/// </summary>
public sealed class EmailConfirmationJobs(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    IRefreshTokenGenerator tokenGenerator,
    IClock clock,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<EmailConfirmationJobs> logger)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public async Task SendConfirmationEmailAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(userId), cancellationToken);
        if (user is null || user.EmailConfirmedAt is not null)
        {
            // Usuario ja confirmou (ou foi removido) entre o enqueue e a execucao —
            // nada a fazer, evita mandar um e-mail redundante ou desnecessario.
            logger.LogInformation("Job de confirmacao de e-mail ignorado: usuario {UserId} nao existe mais ou ja confirmou.", userId);
            return;
        }

        var rawToken = tokenGenerator.GenerateToken();
        user.GenerateEmailConfirmationToken(tokenGenerator.Hash(rawToken), clock.UtcNow.Add(TokenLifetime));
        await dbContext.SaveChangesAsync(cancellationToken);

        var confirmUrl = $"{frontendOptions.Value.BaseUrl}/confirm-email/{rawToken}";
        var html = $"""
            <p>Ola, {user.FullName}!</p>
            <p>Confirme seu e-mail para comecar a usar o Agendio.</p>
            <p><a href="{confirmUrl}">Clique aqui para confirmar seu e-mail</a></p>
            <p>Este link expira em 24 horas.</p>
            """;

        await emailSender.SendAsync(user.Email.Value, "Confirme seu e-mail — Agendio", html, cancellationToken);
    }
}
