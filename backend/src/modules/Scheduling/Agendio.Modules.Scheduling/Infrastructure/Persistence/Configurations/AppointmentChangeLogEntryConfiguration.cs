using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Configurations;

public sealed class AppointmentChangeLogEntryConfiguration : IEntityTypeConfiguration<AppointmentChangeLogEntry>
{
    public void Configure(EntityTypeBuilder<AppointmentChangeLogEntry> builder)
    {
        builder.ToTable("appointment_change_log_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => AppointmentChangeLogEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(e => e.AppointmentId)
            .HasConversion(id => id.Value, value => AppointmentId.From(value))
            .IsRequired();

        builder.Property(e => e.CustomerId).IsRequired();
        builder.Property(e => e.ResourceId).IsRequired();

        builder.Property(e => e.ServiceName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ChangeType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(e => new { e.TenantId, e.AppointmentId, e.OccurredAtUtc });
        builder.HasIndex(e => new { e.TenantId, e.OccurredAtUtc });

        // Filtro de tenant fica no DbContext. Sem soft-delete: log imutavel.
    }
}
