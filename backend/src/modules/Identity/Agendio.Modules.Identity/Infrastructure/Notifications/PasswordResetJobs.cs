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
/// Mesmo molde de EmailConfirmationJobs: token gerado AQUI DENTRO (nunca como
/// argumento do job, que fica em texto plano na tabela do Hangfire ate
/// concluir), so o hash SHA-256 e persistido em User. 30 minutos de validade —
/// bem mais curto que o token de confirmacao de e-mail (24h), porque um pedido
/// de reset nao usado em minutos normalmente significa que a pessoa desistiu ou
/// nao foi ela quem pediu.
/// </summary>
public sealed class PasswordResetJobs(
    IdentityDbContext dbContext,
    ITenantContext tenantContext,
    IRefreshTokenGenerator tokenGenerator,
    IClock clock,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<PasswordResetJobs> logger)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task SendPasswordResetEmailAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(userId), cancellationToken);
        if (user is null || !user.IsActive)
        {
            logger.LogInformation("Job de recuperacao de senha ignorado: usuario {UserId} nao existe mais ou esta inativo.", userId);
            return;
        }

        var rawToken = tokenGenerator.GenerateToken();
        user.GeneratePasswordResetToken(tokenGenerator.Hash(rawToken), clock.UtcNow.Add(TokenLifetime));
        await dbContext.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{frontendOptions.Value.BaseUrl}/reset-password/{rawToken}";
        var html = $"""
            <p>Ola, {user.FullName}!</p>
            <p>Recebemos um pedido para redefinir sua senha no Agendio.</p>
            <p><a href="{resetUrl}">Clique aqui para escolher uma nova senha</a></p>
            <p>Este link expira em 30 minutos. Se voce nao pediu essa redefinicao, pode ignorar este e-mail com seguranca.</p>
            """;

        await emailSender.SendAsync(user.Email.Value, "Redefinir sua senha — Agendio", html, cancellationToken);
    }
}
