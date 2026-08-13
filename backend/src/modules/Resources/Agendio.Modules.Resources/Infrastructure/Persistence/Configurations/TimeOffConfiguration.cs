using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Resources.Infrastructure.Persistence.Configurations;

public sealed class TimeOffConfiguration : IEntityTypeConfiguration<TimeOff>
{
    public void Configure(EntityTypeBuilder<TimeOff> builder)
    {
        builder.ToTable("time_off");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => TimeOffId.From(value))
            .ValueGeneratedNever();

        builder.Property(t => t.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(t => t.ResourceId)
            .HasConversion(id => id.Value, value => ResourceId.From(value))
            .IsRequired();

        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.EndDate).IsRequired();
        builder.Property(t => t.Reason).HasMaxLength(500);

        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(t => new { t.TenantId, t.ResourceId });

        // Filtro de tenant fica no DbContext (ver AgendioDbContextBase).
    }
}
