using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Infrastructure.Notifications;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.ResendConfirmationEmail;

/// <summary>
/// Sempre responde sucesso, exista ou nao o e-mail, ja confirmado ou nao —
/// evita enumeracao de contas. So reenfileira o mesmo job de RegisterUserCommandHandler
/// (que gera um token NOVO, substituindo o anterior — ver EmailConfirmationJobs).
///
/// O rate limit de IP do endpoint (10/60s, ver Program.cs) nao protege um
/// destinatario especifico contra um atacante trocando de IP — por isso o
/// throttle aqui e por (tenant, e-mail), nao por IP. Excedendo a cota, ainda
/// assim responde sucesso generico (nao revela que o limite bateu).
/// </summary>
public sealed class ResendConfirmationEmailCommandHandler(
    IdentityDbContext dbContext, IBackgroundJobClient jobClient, IEmailSendThrottle emailSendThrottle)
    : ICommandHandler<ResendConfirmationEmailCommand>
{
    private const int MaxAttemptsPerHour = 3;
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromHours(1);

    public async Task<Result> Handle(ResendConfirmationEmailCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsSuccess)
        {
            // ExplicitTenantBehavior ja ancorou o tenant no ITenantContext.
            var user = await dbContext.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == emailResult.Value, cancellationToken);

            if (user is not null && user.EmailConfirmedAt is null)
            {
                var throttleKey = $"resend-confirmation:{request.TenantId}:{emailResult.Value.Value}";
                var hasQuota = await emailSendThrottle.TryConsumeAsync(throttleKey, MaxAttemptsPerHour, ThrottleWindow, cancellationToken);

                if (hasQuota)
                {
                    jobClient.Enqueue<EmailConfirmationJobs>(
                        job => job.SendConfirmationEmailAsync(request.TenantId, user.Id.Value, CancellationToken.None));
                }
            }
        }

        return Result.Success();
    }
}
