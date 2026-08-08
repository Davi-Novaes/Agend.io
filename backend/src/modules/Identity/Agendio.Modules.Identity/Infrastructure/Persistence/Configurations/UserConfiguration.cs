using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasMaxLength(320)
            .IsRequired();

        // Unicidade de e-mail e POR TENANT, nunca global — dois estabelecimentos
        // distintos podem ter um cliente/dono com o mesmo e-mail.
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();

        builder.Property(u => u.MfaEnabled).IsRequired().HasDefaultValue(false);

        // Sem HasConversion aqui: MfaSecretEncrypted (como Customer.Cpf/HealthNotes,
        // ver docs/adr/0007) usa EncryptedStringConverter, aplicado no
        // IdentityDbContext.OnModelCreating porque depende de IEncryptionService.

        builder.Property(u => u.CreatedBy).HasMaxLength(256);
        builder.Property(u => u.UpdatedBy).HasMaxLength(256);

        // O filtro de tenant + soft-delete combinados fica no IdentityDbContext,
        // nao aqui: HasQueryFilter SOBRESCREVE (nao combina) filtros anteriores
        // na mesma entidade, e so o DbContext tem acesso ao ITenantContext.
    }
}
