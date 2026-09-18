using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Assistant.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Assistant.Infrastructure.Persistence;

/// <summary>DbContext PROPRIO do modulo Assistant — schema "assistant" isolado. Primeira entidade persistida do modulo (Fase 3 do "Agendio Assist" — antes disso o modulo era 100% stateless).</summary>
public sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options, ITenantContext tenantContext)
    : AgendioDbContextBase(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<AssistantQueryLogEntry> AssistantQueryLogEntries => Set<AssistantQueryLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assistant");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssistantDbContext).Assembly);

        modelBuilder.Entity<AssistantQueryLogEntry>().HasQueryFilter(e => e.TenantId == CurrentTenantId());
    }

    private TenantId CurrentTenantId() => _tenantContext.HasTenant ? _tenantContext.TenantId : TenantId.Empty;
}
