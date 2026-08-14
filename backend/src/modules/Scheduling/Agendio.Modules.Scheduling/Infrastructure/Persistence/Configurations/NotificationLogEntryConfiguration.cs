using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Configurations;

public sealed class NotificationLogEntryConfiguration : IEntityTypeConfiguration<NotificationLogEntry>
{
    public void Configure(EntityTypeBuilder<NotificationLogEntry> builder)
    {
        builder.ToTable("notification_log_entries");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasConversion(id => id.Value, value => NotificationLogEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(n => n.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(n => n.AppointmentId)
            .HasConversion(id => id.Value, value => AppointmentId.From(value))
            .IsRequired();

        builder.Property(n => n.CustomerId).IsRequired();

        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Trigger).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(n => n.SentAtUtc).IsRequired();
        builder.Property(n => n.ErrorMessage).HasMaxLength(2000);

        builder.Property(n => n.CreatedBy).HasMaxLength(256);
        builder.Property(n => n.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(n => new { n.TenantId, n.AppointmentId });
        builder.HasIndex(n => new { n.TenantId, n.SentAtUtc });

        // Filtro de tenant fica no DbContext. Sem soft-delete: e um log
        // imutavel, nunca "apagado" pela aplicacao (mesmo padrao de StockMovement).
    }
}
