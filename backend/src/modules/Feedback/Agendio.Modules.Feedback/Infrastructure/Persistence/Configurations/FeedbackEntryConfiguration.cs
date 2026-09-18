using Agendio.Modules.Feedback.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Feedback.Infrastructure.Persistence.Configurations;

public sealed class FeedbackEntryConfiguration : IEntityTypeConfiguration<FeedbackEntry>
{
    public void Configure(EntityTypeBuilder<FeedbackEntry> builder)
    {
        builder.ToTable("feedback_entries");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasConversion(id => id.Value, value => FeedbackEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(f => f.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        // Guid cru, sem FK de banco (cross-schema, mesmo motivo de
        // Resource.UnitId nao ter FK para tenancy.units) — so precisa dizer
        // quem enviou, ninguem consulta por este campo ainda.
        builder.Property(f => f.SubmittedByUserId).IsRequired();

        builder.Property(f => f.Subject).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Body).IsRequired().HasMaxLength(4000);

        builder.Property(f => f.CreatedBy).HasMaxLength(256);
        builder.Property(f => f.UpdatedBy).HasMaxLength(256);

        // Sem Global Query Filter de proposito -- FeedbackEntry nao e
        // ITenantOwned (ver comentario na entidade): o painel do Super Admin
        // precisa ler feedback de todos os tenants.
    }
}
