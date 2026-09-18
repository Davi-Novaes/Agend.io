using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Feedback.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Feedback.Infrastructure.Persistence;

/// <summary>
/// DbContext PROPRIO do modulo Feedback — schema "feedback" isolado. Sem
/// ITenantContext no construtor: FeedbackEntry deliberadamente nao e
/// ITenantOwned (ver comentario na entidade), entao nao ha Global Query Filter
/// a montar — mesmo raciocinio de BillingDbContext/PlatformDbContext.
/// </summary>
public sealed class FeedbackDbContext(DbContextOptions<FeedbackDbContext> options) : AgendioDbContextBase(options)
{
    public DbSet<FeedbackEntry> FeedbackEntries => Set<FeedbackEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("feedback");

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FeedbackDbContext).Assembly);
    }
}
