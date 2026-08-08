using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("mfa_recovery_codes");

        builder.HasKey(rc => rc.Id);
        builder.Property(rc => rc.Id)
            .HasConversion(id => id.Value, value => MfaRecoveryCodeId.From(value))
            .ValueGeneratedNever();

        builder.Property(rc => rc.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(rc => rc.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .IsRequired();

        builder.Property(rc => rc.CodeHash).IsRequired().HasMaxLength(64);

        // Hash SHA-256 em hex e globalmente unico por construcao (mesmo raciocinio
        // de RefreshTokenConfiguration) — o indice unico e defesa extra.
        builder.HasIndex(rc => rc.CodeHash).IsUnique();
        builder.HasIndex(rc => new { rc.TenantId, rc.UserId });

        builder.Property(rc => rc.CreatedAtUtc).IsRequired();
    }
}
