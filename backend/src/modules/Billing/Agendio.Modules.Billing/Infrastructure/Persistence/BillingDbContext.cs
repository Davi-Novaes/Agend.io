using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Billing — schema "billing" isolado. Sem
/// ITenantContext no construtor: nenhuma entidade daqui e ITenantOwned (ver
/// comentario em Subscription.cs), entao nao ha Global Query Filter a montar —
/// mesmo raciocinio de PlatformDbContext/TenancyDbContext.
/// </summary>
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : AgendioDbContextBase(options)
{
    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}
