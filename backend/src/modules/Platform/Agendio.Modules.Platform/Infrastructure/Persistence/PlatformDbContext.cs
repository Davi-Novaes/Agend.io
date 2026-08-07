using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Platform — schema "platform" isolado. Sem
/// entidade ITenantOwned nenhuma (PlatformAdmin fica fora de qualquer tenant),
/// entao nao ha Global Query Filter aqui — ao contrario de todo outro DbContext
/// de modulo, este nem precisa de ITenantContext no construtor.
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : AgendioDbContextBase(options)
{
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
