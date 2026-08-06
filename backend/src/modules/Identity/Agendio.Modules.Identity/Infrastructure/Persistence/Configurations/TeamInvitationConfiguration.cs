using Agendio.Modules.Identity.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class TeamInvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> builder)
    {
        builder.ToTable("team_invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => TeamInvitationId.From(value))
            .ValueGeneratedNever();

        builder.Property(i => i.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(i => i.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(64);

        // Mesma logica do RefreshToken.TokenHash: SHA-256 hex de um segredo de
        // alta entropia, indice unico como defesa extra (nao a garantia principal).
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Email });

        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.ExpiresAtUtc).IsRequired();
    }
}
