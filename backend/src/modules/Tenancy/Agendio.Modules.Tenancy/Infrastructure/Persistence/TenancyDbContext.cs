using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Tenancy — schema "tenancy" isolado. Nenhum outro
/// modulo tem referencia a esta classe; leitura sincrona de outro modulo passa
/// por ITenantLookupService (Agendio.Modules.Tenancy.Contracts).
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options) : AgendioDbContextBase(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenancy");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
    }
}
