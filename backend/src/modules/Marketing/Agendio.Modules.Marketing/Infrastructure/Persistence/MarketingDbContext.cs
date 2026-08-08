using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Marketing.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Marketing.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Marketing — schema "marketing" isolado.</summary>
public sealed class MarketingDbContext(DbContextOptions<MarketingDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("marketing");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketingDbContext).Assembly);

        // Sem soft-delete: Campaign e um log imutavel, so filtra por tenant.
        modelBuilder.Entity<Campaign>().HasQueryFilter(c => c.TenantId == CurrentTenantId());
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
