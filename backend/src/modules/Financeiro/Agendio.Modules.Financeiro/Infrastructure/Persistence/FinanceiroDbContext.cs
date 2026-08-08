using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Financeiro — schema "financeiro" isolado.</summary>
public sealed class FinanceiroDbContext(DbContextOptions<FinanceiroDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<AccountReceivable> AccountsReceivable => Set<AccountReceivable>();

    public DbSet<AccountPayable> AccountsPayable => Set<AccountPayable>();

    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("financeiro");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceiroDbContext).Assembly);

        // Um unico HasQueryFilter por entidade: chamar de novo SOBRESCREVE (nao
        // combina) o filtro anterior — tenant e soft-delete sempre juntos aqui.
        modelBuilder.Entity<AccountReceivable>().HasQueryFilter(a => a.TenantId == CurrentTenantId() && !a.IsDeleted);
        modelBuilder.Entity<AccountPayable>().HasQueryFilter(a => a.TenantId == CurrentTenantId() && !a.IsDeleted);
        modelBuilder.Entity<CommissionRule>().HasQueryFilter(c => c.TenantId == CurrentTenantId());
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
