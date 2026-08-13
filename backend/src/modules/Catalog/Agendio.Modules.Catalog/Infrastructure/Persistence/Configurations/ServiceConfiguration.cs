using Agendio.Modules.Catalog.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Catalog.Infrastructure.Persistence.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => ServiceId.From(value))
            .ValueGeneratedNever();

        builder.Property(s => s.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.DurationMinutes).IsRequired();
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.DisplayOrder).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.ImageUrl).HasMaxLength(500);
        builder.Property(s => s.IsActive).IsRequired();

        // Money e Value Object com dois campos — mapeado como owned type (duas
        // colunas na mesma tabela), nao HasConversion (que so serve para um
        // unico valor escalar de ida e volta).
        builder.OwnsOne(s => s.Price, price =>
        {
            price.Property(p => p.Amount).HasColumnName("price_amount").HasColumnType("numeric(10,2)").IsRequired();
            price.Property(p => p.Currency).HasColumnName("price_currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(s => new { s.TenantId, s.Name });

        // Filtro de tenant + soft-delete fica no DbContext (ver AgendioDbContextBase).
    }
}
