using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id)
            .HasConversion(id => id.Value, value => RefreshTokenId.From(value))
            .ValueGeneratedNever();

        builder.Property(rt => rt.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(rt => rt.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .IsRequired();

        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(64);

        // Hash SHA-256 em hex e globalmente unico por construcao (alta entropia do
        // token de origem) — o indice unico e defesa extra, nao a garantia principal.
        builder.HasIndex(rt => rt.TokenHash).IsUnique();
        builder.HasIndex(rt => rt.FamilyId);
        builder.HasIndex(rt => rt.UserId);

        builder.Property(rt => rt.FamilyId).IsRequired();
        builder.Property(rt => rt.CreatedAtUtc).IsRequired();
        builder.Property(rt => rt.ExpiresAtUtc).IsRequired();
    }
}
