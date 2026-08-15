using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Configurations;

public sealed class AppointmentDepositConfiguration : IEntityTypeConfiguration<AppointmentDeposit>
{
    public void Configure(EntityTypeBuilder<AppointmentDeposit> builder)
    {
        builder.ToTable("appointment_deposits");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => AppointmentDepositId.From(value))
            .ValueGeneratedNever();

        builder.Property(d => d.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(d => d.AppointmentId)
            .HasConversion(id => id.Value, value => AppointmentId.From(value))
            .IsRequired();

        builder.OwnsOne(d => d.Amount, amount =>
        {
            amount.Property(a => a.Amount).HasColumnName("amount").HasColumnType("numeric(10,2)").IsRequired();
            amount.Property(a => a.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(d => d.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(d => d.GatewayChargeId).HasMaxLength(100);
        builder.Property(d => d.InvoiceUrl).HasMaxLength(500);

        builder.Property(d => d.CreatedBy).HasMaxLength(256);
        builder.Property(d => d.UpdatedBy).HasMaxLength(256);

        // Um deposito por agendamento (o agendamento so cria um, mesmo se o gateway falhar e for retentado manualmente).
        builder.HasIndex(d => d.AppointmentId).IsUnique();

        // Usado pelo webhook para achar o deposito pela cobranca do gateway.
        builder.HasIndex(d => new { d.TenantId, d.GatewayChargeId });
    }
}
