using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Estoque — schema "estoque" isolado.</summary>
public sealed class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("estoque");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EstoqueDbContext).Assembly);

        // Um unico HasQueryFilter por entidade: chamar de novo SOBRESCREVE (nao
        // combina) o filtro anterior. StockMovement nao e ISoftDeletable (log
        // imutavel) — so filtra por tenant, igual CommissionRule no Financeiro.
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.TenantId == CurrentTenantId() && !p.IsDeleted);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(m => m.TenantId == CurrentTenantId());
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
