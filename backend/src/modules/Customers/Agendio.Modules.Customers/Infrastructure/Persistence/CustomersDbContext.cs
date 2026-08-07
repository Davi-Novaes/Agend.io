using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Customers.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Customers — schema "customers" isolado.</summary>
public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("customers");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);

        modelBuilder.Entity<Customer>().HasQueryFilter(c => c.TenantId == CurrentTenantId() && !c.IsDeleted);
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
