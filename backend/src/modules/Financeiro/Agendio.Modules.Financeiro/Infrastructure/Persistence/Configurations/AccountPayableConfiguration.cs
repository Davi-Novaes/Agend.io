using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Financeiro.Infrastructure.Persistence.Configurations;

public sealed class AccountPayableConfiguration : IEntityTypeConfiguration<AccountPayable>
{
    public void Configure(EntityTypeBuilder<AccountPayable> builder)
    {
        builder.ToTable("accounts_payable");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => AccountPayableId.From(value))
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(a => a.Description).IsRequired().HasMaxLength(500);
        builder.Property(a => a.DueDate).IsRequired();
        builder.Property(a => a.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.OwnsOne(a => a.Amount, amount =>
        {
            amount.Property(m => m.Amount).HasColumnName("amount").HasColumnType("numeric(10,2)").IsRequired();
            amount.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(a => new { a.TenantId, a.Status });
        builder.HasIndex(a => new { a.TenantId, a.Category });

        // Mesma logica de AccountReceivableConfiguration: indice unico parcial so
        // para as entradas de comissao geradas automaticamente (SourceAppointmentId
        // preenchido) — despesas manuais (null) nao entram na restricao.
        builder.HasIndex(a => a.SourceAppointmentId).IsUnique().HasFilter("source_appointment_id IS NOT NULL");

        // Filtro de tenant + soft-delete fica no DbContext (ver AgendioDbContextBase).
    }
}
