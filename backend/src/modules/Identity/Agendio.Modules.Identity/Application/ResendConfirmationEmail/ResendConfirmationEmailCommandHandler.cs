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
/// </summary>
public sealed class ResendConfirmationEmailCommandHandler(IdentityDbContext dbContext, IBackgroundJobClient jobClient)
    : ICommandHandler<ResendConfirmationEmailCommand>
{
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
                jobClient.Enqueue<EmailConfirmationJobs>(
                    job => job.SendConfirmationEmailAsync(request.TenantId, user.Id.Value, CancellationToken.None));
            }
        }

        return Result.Success();
    }
}
