using Agendio.Modules.Customers.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Customers.Infrastructure.Persistence.Configurations;

public sealed class LoyaltyPointsLedgerEntryConfiguration : IEntityTypeConfiguration<LoyaltyPointsLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LoyaltyPointsLedgerEntry> builder)
    {
        builder.ToTable("loyalty_points_ledger_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => LoyaltyPointsLedgerEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(e => e.CustomerId)
            .HasConversion(id => id.Value, value => CustomerId.From(value))
            .IsRequired();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(e => new { e.TenantId, e.CustomerId });

        // Indice unico PARCIAL: idempotencia contra redelivery do evento
        // AppointmentCompleted pelo RabbitMQ — o mesmo agendamento nunca gera
        // dois lancamentos Earned, mas nao trava resgates (Kind = Redeemed,
        // AppointmentId sempre null) de coexistir a vontade.
        builder.HasIndex(e => new { e.TenantId, e.AppointmentId })
            .IsUnique()
            .HasFilter("kind = 'Earned'");

        // Filtro de tenant fica no DbContext (ver AgendioDbContextBase) — sem
        // ISoftDeletable de proposito, e um log imutavel.
    }
}
