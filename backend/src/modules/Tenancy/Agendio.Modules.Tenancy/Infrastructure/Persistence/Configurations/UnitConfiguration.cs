using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Configurations;

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("units");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => UnitId.From(value))
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Address).HasMaxLength(500);
        builder.Property(u => u.City).HasMaxLength(150);
        builder.Property(u => u.State).HasMaxLength(2);
        builder.Property(u => u.Country).HasMaxLength(100);
        builder.Property(u => u.Phone).HasMaxLength(30);
        builder.Property(u => u.WhatsApp).HasMaxLength(30);
        builder.Property(u => u.IsActive).IsRequired();

        builder.OwnsMany(u => u.BusinessHours, businessHours =>
        {
            businessHours.ToTable("unit_business_hours");
            businessHours.WithOwner().HasForeignKey("unit_id");
            businessHours.Property<int>("id");
            businessHours.HasKey("id").HasName("pk_unit_business_hours");
            businessHours.Property(h => h.DayOfWeek).HasConversion<string>().HasMaxLength(20).IsRequired();
            businessHours.Property(h => h.StartTime).IsRequired();
            businessHours.Property(h => h.EndTime).IsRequired();
        });

        builder.Property(u => u.CreatedBy).HasMaxLength(256);
        builder.Property(u => u.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(u => new { u.TenantId, u.Name });

        // Filtro de tenant + soft-delete fica no DbContext (ver TenancyDbContext).
    }
}
