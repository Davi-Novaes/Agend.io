using Agendio.Infrastructure.Security;
using Agendio.Modules.Customers.Infrastructure.Notifications;
using Hangfire;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

/// <summary>
/// Compartilhado entre pedir codigo (cliente ja existente) e criar conta —
/// mesmo limite antiabuso e mesmo job de envio nos dois casos.
/// </summary>
public sealed class CustomerPortalAccessCodeSender(IBackgroundJobClient jobClient, IEmailSendThrottle emailSendThrottle)
{
    private const int MaxAttemptsPerHour = 3;
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromHours(1);

    public async Task SendIfQuotaAvailableAsync(Guid tenantId, Guid customerId, string normalizedEmail, CancellationToken cancellationToken)
    {
        var throttleKey = $"customer-portal:{tenantId}:{normalizedEmail}";
        var hasQuota = await emailSendThrottle.TryConsumeAsync(
            throttleKey, MaxAttemptsPerHour, ThrottleWindow, cancellationToken);

        if (hasQuota)
        {
            jobClient.Enqueue<CustomerPortalAccessEmailJob>(job => job.SendAccessCodeAsync(
                tenantId, customerId, CancellationToken.None));
        }
    }
}
