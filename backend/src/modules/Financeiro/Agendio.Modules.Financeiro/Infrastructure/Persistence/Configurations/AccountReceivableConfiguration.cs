using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Financeiro.Infrastructure.Persistence.Configurations;

public sealed class AccountReceivableConfiguration : IEntityTypeConfiguration<AccountReceivable>
{
    public void Configure(EntityTypeBuilder<AccountReceivable> builder)
    {
        builder.ToTable("accounts_receivable");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => AccountReceivableId.From(value))
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(a => a.Description).IsRequired().HasMaxLength(500);
        builder.Property(a => a.DueDate).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.OwnsOne(a => a.Amount, amount =>
        {
            amount.Property(m => m.Amount).HasColumnName("amount").HasColumnType("numeric(10,2)").IsRequired();
            amount.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(a => new { a.TenantId, a.Status });

        // Indice unico PARCIAL: so vale quando SourceAppointmentId nao e nulo —
        // impede o consumidor de duplicar a receita do mesmo agendamento em
        // redelivery do evento, sem impedir varias entradas manuais (null) de coexistir.
        builder.HasIndex(a => a.SourceAppointmentId).IsUnique().HasFilter("source_appointment_id IS NOT NULL");

        // Filtro de tenant + soft-delete fica no DbContext (ver AgendioDbContextBase).
    }
}
