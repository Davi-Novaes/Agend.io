using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Scheduling — schema "scheduling" isolado.</summary>
public sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("scheduling");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulingDbContext).Assembly);

        modelBuilder.Entity<Appointment>().HasQueryFilter(a => a.TenantId == CurrentTenantId());
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
