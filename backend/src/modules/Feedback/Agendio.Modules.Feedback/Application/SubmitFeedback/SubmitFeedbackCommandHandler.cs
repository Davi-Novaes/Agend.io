using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Feedback.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Feedback.Application.SubmitFeedback;

public sealed class SubmitFeedbackCommandHandler(
    FeedbackDbContext dbContext,
    ITenantContext tenantContext,
    ITenantLookupService tenantLookupService,
    IEmailSender emailSender,
    IOptions<FeedbackNotificationOptions> notificationOptions,
    ILogger<SubmitFeedbackCommandHandler> logger)
    : ICommandHandler<SubmitFeedbackCommand>
{
    public async Task<Result> Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
    {
        var entryResult = Domain.FeedbackEntry.Create(tenantContext.TenantId, request.SubmittedByUserId, request.Subject, request.Body);
        if (entryResult.IsFailure)
        {
            return entryResult;
        }

        dbContext.FeedbackEntries.Add(entryResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyAsync(entryResult.Value.Subject, entryResult.Value.Body, cancellationToken);

        return Result.Success();
    }

    // Falha de e-mail nao derruba o feedback: ele ja esta salvo e continua
    // visivel no painel do Super Admin (ver GetFeedbackForPlatformQueryHandler)
    // mesmo que a notificacao nao chegue — por isso so logamos, sem propagar.
    private async Task TryNotifyAsync(string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await tenantLookupService.FindByIdAsync(tenantContext.TenantId, cancellationToken);
            var tenantName = tenant?.Name ?? "estabelecimento desconhecido";

            var html = $"""
                <p>Novo feedback recebido de <strong>{tenantName}</strong>.</p>
                <p><strong>Assunto:</strong> {subject}</p>
                <p><strong>Mensagem:</strong><br />{body}</p>
                """;

            await emailSender.SendAsync(
                notificationOptions.Value.NotificationEmail, $"Novo feedback — {tenantName}", html, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar notificacao de feedback por e-mail. O registro continua salvo.");
        }
    }
}
