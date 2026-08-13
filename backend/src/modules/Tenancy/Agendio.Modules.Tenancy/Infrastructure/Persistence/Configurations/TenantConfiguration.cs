using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);

        builder.Property(t => t.Slug)
            .HasConversion(slug => slug.Value, value => Slug.Create(value).Value)
            .HasMaxLength(63)
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        // Default de banco (nao so na aplicacao) para tenants que ja existiam
        // antes desta coluna: ficam classificados como "Other" em vez de
        // quebrar a migration.
        builder.Property(t => t.BusinessType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue(BusinessType.Other);

        builder.Property(t => t.TimeZoneId).IsRequired().HasMaxLength(64);
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.PrimaryColorHex).HasMaxLength(7);
        builder.Property(t => t.LogoUrl).HasMaxLength(500);

        builder.Property(t => t.Description).HasMaxLength(2000);

        builder.Property(t => t.Phone)
            .HasConversion(
                phone => phone == null ? null : phone.Value,
                value => value == null ? null : PhoneNumber.Create(value).Value)
            .HasMaxLength(20);

        builder.Property(t => t.WhatsApp)
            .HasConversion(
                phone => phone == null ? null : phone.Value,
                value => value == null ? null : PhoneNumber.Create(value).Value)
            .HasMaxLength(20);

        builder.Property(t => t.Email)
            .HasConversion(
                email => email == null ? null : email.Value,
                value => value == null ? null : Email.Create(value).Value)
            .HasMaxLength(320);

        builder.Property(t => t.Address).HasMaxLength(500);
        builder.Property(t => t.InstagramUrl).HasMaxLength(500);
        builder.Property(t => t.FacebookUrl).HasMaxLength(500);

        // Colecao de Value Objects sem identidade propria — tabela filha, FK
        // sombra gerada pelo EF, sem repositorio separado (mesmo padrao de
        // Resource.WorkingHours no modulo Resources).
        builder.OwnsMany(t => t.BusinessHours, businessHours =>
        {
            businessHours.ToTable("tenant_business_hours");
            businessHours.WithOwner().HasForeignKey("tenant_id");
            businessHours.Property<int>("id");
            businessHours.HasKey("id");

            businessHours.Property(h => h.DayOfWeek).HasConversion<string>().HasMaxLength(20).IsRequired();
            businessHours.Property(h => h.StartTime).IsRequired();
            businessHours.Property(h => h.EndTime).IsRequired();
        });

        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        // Tenant nao e ITenantOwned (nao ha RLS por TenantId aqui), mas ainda
        // esconde registros desativados via soft-delete.
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
