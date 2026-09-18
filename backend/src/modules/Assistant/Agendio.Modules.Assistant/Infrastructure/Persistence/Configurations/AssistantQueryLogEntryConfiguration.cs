using Agendio.Modules.Assistant.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Assistant.Infrastructure.Persistence.Configurations;

public sealed class AssistantQueryLogEntryConfiguration : IEntityTypeConfiguration<AssistantQueryLogEntry>
{
    public void Configure(EntityTypeBuilder<AssistantQueryLogEntry> builder)
    {
        builder.ToTable("assistant_query_log");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => AssistantQueryLogEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.QuestionExcerpt).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ResolvedIntent).HasMaxLength(64);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(16);
        builder.Property(e => e.ToolOrServiceUsed).HasMaxLength(128);
        builder.Property(e => e.ErrorCode).HasMaxLength(128);
        builder.Property(e => e.OccurredAtUtc).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.OccurredAtUtc });

        // Filtro de tenant fica no DbContext (ver AssistantDbContext).
    }
}
