using Agendio.Modules.Marketing.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Marketing.Infrastructure.Persistence.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => CampaignId.From(value))
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(c => c.Subject).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Body).IsRequired().HasMaxLength(10000);
        builder.Property(c => c.RecipientCount).IsRequired();
        builder.Property(c => c.SentAtUtc).IsRequired();

        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(c => new { c.TenantId, c.SentAtUtc });

        // Filtro de tenant fica no DbContext (ver AgendioDbContextBase). Sem
        // soft-delete: Campaign e um log imutavel, nunca apagado.
    }
}
