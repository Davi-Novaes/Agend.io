using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Estoque.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => StockMovementId.From(value))
            .ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(m => m.ProductId).IsRequired();
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Quantity).IsRequired();
        builder.Property(m => m.Reason).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Notes).HasMaxLength(500);
        builder.Property(m => m.OccurredAtUtc).IsRequired();

        builder.Property(m => m.CreatedBy).HasMaxLength(256);
        builder.Property(m => m.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(m => new { m.TenantId, m.ProductId });
        builder.HasIndex(m => new { m.TenantId, m.OccurredAtUtc });

        // Filtro de tenant fica no DbContext (ver AgendioDbContextBase). Sem
        // soft-delete: StockMovement e um log imutavel, nunca apagado.
    }
}
