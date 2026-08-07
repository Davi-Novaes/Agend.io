using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Catalog.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Catalog — schema "catalog" isolado.</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        modelBuilder.Entity<Service>().HasQueryFilter(s => s.TenantId == CurrentTenantId() && !s.IsDeleted);
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
