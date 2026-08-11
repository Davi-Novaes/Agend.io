using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Tenancy — schema "tenancy" isolado. Nenhum outro
/// modulo tem referencia a esta classe; leitura sincrona de outro modulo passa
/// por ITenantLookupService/IUnitLookupService (Agendio.Modules.Tenancy.Contracts).
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Unit> Units => Set<Unit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenancy");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);

        // Tenant deliberadamente NAO tem filtro de tenant — e o proprio tenant.
        modelBuilder.Entity<Unit>().HasQueryFilter(u => u.TenantId == CurrentTenantId() && !u.IsDeleted);
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
