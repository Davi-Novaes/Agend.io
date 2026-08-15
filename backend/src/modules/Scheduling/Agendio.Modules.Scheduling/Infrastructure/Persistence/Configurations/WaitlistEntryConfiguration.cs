using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Configurations;

public sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("waitlist_entries");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => WaitlistEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(w => w.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(w => w.CustomerId).IsRequired();
        builder.Property(w => w.ResourceId);
        builder.Property(w => w.ServiceId).IsRequired();

        builder.Property(w => w.ServiceName).IsRequired().HasMaxLength(200);
        builder.Property(w => w.PreferredDate).IsRequired();
        builder.Property(w => w.Notes).HasMaxLength(500);

        builder.Property(w => w.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(w => w.ConvertedAppointmentId)
            .HasConversion(id => id!.Value, value => AppointmentId.From(value));

        builder.Property(w => w.CreatedBy).HasMaxLength(256);
        builder.Property(w => w.UpdatedBy).HasMaxLength(256);

        // Usado para achar candidatos elegiveis quando uma vaga abre (mesmo servico/data, aguardando).
        builder.HasIndex(w => new { w.TenantId, w.ServiceId, w.PreferredDate, w.Status });

        // Usado pela listagem da equipe (mais recentes/aguardando primeiro).
        builder.HasIndex(w => new { w.TenantId, w.Status, w.CreatedAtUtc });

        // Filtro de tenant fica no DbContext. Sem soft-delete: status carrega o historico (Cancelled/Converted permanecem).
    }
}
