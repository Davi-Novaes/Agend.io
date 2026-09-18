using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Tenancy.Infrastructure.Jobs;

/// <summary>
/// Roda diariamente (ver registro em Program.cs), mesmo molde de
/// BillingReconciliationJob. Libera o slug de tenants abandonados no meio do
/// wizard de cadastro (criados ha mais de 48h, nunca tiveram um usuario
/// registrado) — sem isso, o slug fica permanentemente reservado por alguem
/// que nunca completou o cadastro (P1-1, docs/AUTH_BILLING_SECURITY_AUDIT.md).
///
/// So mexe na tabela tenancy.tenants — nao ha exclusao em cascata de dados de
/// outros modulos (ver comentario em Tenant.ReleaseAbandonedSlug).
/// </summary>
public sealed class OrphanTenantCleanupJob(TenancyDbContext dbContext, IClock clock, ILogger<OrphanTenantCleanupJob> logger)
{
    private const int AbandonmentThresholdHours = 48;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow.AddHours(-AbandonmentThresholdHours);

        var abandonedTenants = await dbContext.Tenants
            .Where(t => t.IsActive && t.FirstUserRegisteredAtUtc == null && t.CreatedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var tenant in abandonedTenants)
        {
            var releasedSlug = tenant.Slug.Value;
            var result = tenant.ReleaseAbandonedSlug();
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Falha ao liberar o slug do tenant {TenantId}: {Error}", tenant.Id.Value, result.Error.Message);
                continue;
            }

            logger.LogInformation(
                "Slug '{Slug}' liberado — tenant {TenantId} abandonado ha mais de {Hours}h sem nenhum usuario registrado.",
                releasedSlug, tenant.Id.Value, AbandonmentThresholdHours);
        }

        if (abandonedTenants.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
