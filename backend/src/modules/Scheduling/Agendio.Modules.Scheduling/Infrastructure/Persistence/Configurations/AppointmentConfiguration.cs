using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => AppointmentId.From(value))
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(a => a.CustomerId).IsRequired();
        builder.Property(a => a.ResourceId).IsRequired();
        builder.Property(a => a.UnitId);
        builder.Property(a => a.ServiceId).IsRequired();
        builder.Property(a => a.ServiceName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(2000);

        // TimeSlot vira duas colunas simples — a coluna tstzrange usada pela
        // EXCLUDE constraint (ver migration) e gerada pelo Postgres a partir
        // delas, o EF nunca escreve nela diretamente.
        builder.OwnsOne(a => a.Slot, slot =>
        {
            slot.Property(s => s.StartUtc).HasColumnName("start_at_utc").IsRequired();
            slot.Property(s => s.EndUtc).HasColumnName("end_at_utc").IsRequired();
        });

        builder.OwnsOne(a => a.Price, price =>
        {
            price.Property(p => p.Amount).HasColumnName("price_amount").HasColumnType("numeric(10,2)").IsRequired();
            price.Property(p => p.Currency).HasColumnName("price_currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(a => new { a.TenantId, a.ResourceId, a.CustomerId });

        // Filtro de tenant fica no DbContext (ver AgendioDbContextBase). Sem
        // soft-delete aqui: cancelar E o "delete" — o status ja carrega isso.
    }
}
