using Agendio.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class PlatformAdminConfiguration : IEntityTypeConfiguration<PlatformAdmin>
{
    public void Configure(EntityTypeBuilder<PlatformAdmin> builder)
    {
        builder.ToTable("platform_admins");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => PlatformAdminId.From(value))
            .ValueGeneratedNever();

        builder.Property(a => a.Email).IsRequired().HasMaxLength(320);
        builder.Property(a => a.FullName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.PasswordHash).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(a => a.Email).IsUnique();
    }
}
