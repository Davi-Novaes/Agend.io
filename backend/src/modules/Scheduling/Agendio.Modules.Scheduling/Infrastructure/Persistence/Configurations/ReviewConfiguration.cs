using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => ReviewId.From(value))
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(r => r.AppointmentId)
            .HasConversion(id => id.Value, value => AppointmentId.From(value))
            .IsRequired();

        builder.Property(r => r.CustomerId).IsRequired();
        builder.Property(r => r.ResourceId).IsRequired();

        builder.Property(r => r.ServiceName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(1000);

        builder.Property(r => r.CreatedBy).HasMaxLength(256);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        // No maximo um review por agendamento.
        builder.HasIndex(r => new { r.TenantId, r.AppointmentId }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.CreatedAtUtc });
        builder.HasIndex(r => new { r.TenantId, r.ResourceId });

        // Filtro de tenant fica no DbContext. Sem soft-delete: log imutavel.
    }
}
