using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Infrastructure.Notifications;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.ForgotPassword;

/// <summary>
/// Sempre responde sucesso, exista ou nao o e-mail — mesmo raciocinio de
/// ResendConfirmationEmailCommandHandler, evita enumeracao de contas. So
/// enfileira o job que gera o token (PasswordResetJobs) quando o usuario existe
/// e esta ativo.
///
/// Throttle por (tenant, e-mail) alem do rate limit de IP do endpoint — ver
/// comentario equivalente em ResendConfirmationEmailCommandHandler (P1-7).
/// </summary>
public sealed class ForgotPasswordCommandHandler(
    IdentityDbContext dbContext, IBackgroundJobClient jobClient, IEmailSendThrottle emailSendThrottle, SecurityAuditLogger<IdentityDbContext> securityAuditLogger)
    : ICommandHandler<ForgotPasswordCommand>
{
    private const int MaxAttemptsPerHour = 3;
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromHours(1);

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsSuccess)
        {
            // ExplicitTenantBehavior ja ancorou o tenant no ITenantContext.
            var user = await dbContext.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == emailResult.Value, cancellationToken);

            if (user is not null && user.IsActive)
            {
                var throttleKey = $"forgot-password:{request.TenantId}:{emailResult.Value.Value}";
                var hasQuota = await emailSendThrottle.TryConsumeAsync(throttleKey, MaxAttemptsPerHour, ThrottleWindow, cancellationToken);

                if (hasQuota)
                {
                    jobClient.Enqueue<PasswordResetJobs>(
                        job => job.SendPasswordResetEmailAsync(request.TenantId, user.Id.Value, CancellationToken.None));

                    await securityAuditLogger.LogAsync("PasswordResetRequested", success: true, request.TenantId, user.Id.Value, null, cancellationToken);
                }
            }
        }

        return Result.Success();
    }
}
