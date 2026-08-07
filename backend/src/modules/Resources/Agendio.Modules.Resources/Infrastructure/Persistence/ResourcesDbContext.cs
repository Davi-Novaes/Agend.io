using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Resources — schema "resources" isolado.</summary>
public sealed class ResourcesDbContext(DbContextOptions<ResourcesDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Resource> Resources => Set<Resource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("resources");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResourcesDbContext).Assembly);

        modelBuilder.Entity<Resource>().HasQueryFilter(r => r.TenantId == CurrentTenantId() && !r.IsDeleted);
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
