using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Customers.Application.CustomerPortal;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Customers.Infrastructure.Notifications;

public sealed class CustomerPortalAccessEmailJob(
    CustomersDbContext dbContext,
    ITenantContext tenantContext,
    ICustomerPortalAccessStore accessStore,
    ITenantLookupService tenantLookup,
    IEmailSender emailSender,
    ILogger<CustomerPortalAccessEmailJob> logger)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    public async Task SendAccessCodeAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == CustomerId.From(customerId), cancellationToken);
        if (customer is null || !customer.IsActive || customer.Email is null)
        {
            logger.LogInformation("Envio de acesso ao portal ignorado para cliente inativo ou inexistente {CustomerId}.", customerId);
            return;
        }

        var tenant = await tenantLookup.FindByIdAsync(TenantId.From(tenantId), cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        await accessStore.StoreChallengeAsync(tenantId, customer.Email.Value, customerId, codeHash, CodeLifetime);

        var customerName = WebUtility.HtmlEncode(customer.FullName);
        var tenantName = WebUtility.HtmlEncode(tenant.Name);
        var html = $"""
            <p>Olá, {customerName}!</p>
            <p>Use o código abaixo para acessar seus agendamentos em <strong>{tenantName}</strong>:</p>
            <p style="font-size: 28px; font-weight: 700; letter-spacing: 6px;">{code}</p>
            <p>O código expira em 10 minutos e só pode ser usado uma vez.</p>
            <p>Se você não pediu este acesso, ignore este e-mail.</p>
            """;

        await emailSender.SendAsync(customer.Email.Value, $"Seu código de acesso — {tenant.Name}", html, cancellationToken);
    }
}
