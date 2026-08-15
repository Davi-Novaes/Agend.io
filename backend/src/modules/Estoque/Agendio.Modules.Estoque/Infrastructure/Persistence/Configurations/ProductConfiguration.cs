using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Estoque.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => ProductId.From(value))
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Sku).HasMaxLength(50);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.QuantityInStock).IsRequired();
        builder.Property(p => p.MinimumStock).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        // CostPrice/SalePrice sao opcionais (produto pode nao ter preco de custo
        // ou de venda cadastrado ainda) — diferente de todo outro uso de Money no
        // projeto, que e sempre obrigatorio. IsRequired(false) explicito porque
        // OwnsOne assume dependente obrigatorio por padrao.
        builder.OwnsOne(p => p.CostPrice, costPrice =>
        {
            costPrice.Property(m => m.Amount).HasColumnName("cost_price_amount").HasColumnType("numeric(10,2)");
            costPrice.Property(m => m.Currency).HasColumnName("cost_price_currency").HasMaxLength(3);
        });
        builder.Navigation(p => p.CostPrice).IsRequired(false);

        builder.OwnsOne(p => p.SalePrice, salePrice =>
        {
            salePrice.Property(m => m.Amount).HasColumnName("sale_price_amount").HasColumnType("numeric(10,2)");
            salePrice.Property(m => m.Currency).HasColumnName("sale_price_currency").HasMaxLength(3);
        });
        builder.Navigation(p => p.SalePrice).IsRequired(false);

        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder.HasIndex(p => new { p.TenantId, p.Sku });

        // Filtro de tenant + soft-delete fica no DbContext (ver AgendioDbContextBase).
    }
}
