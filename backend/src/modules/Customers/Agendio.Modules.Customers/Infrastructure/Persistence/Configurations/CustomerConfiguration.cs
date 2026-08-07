using System.Text.Json;
using Agendio.Modules.Customers.Domain;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Customers.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => CustomerId.From(value))
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(c => c.FullName).IsRequired().HasMaxLength(200);

        builder.Property(c => c.Email)
            .HasConversion(
                email => email == null ? null : email.Value,
                value => value == null ? null : Email.Create(value).Value)
            .HasMaxLength(320);

        builder.Property(c => c.Phone)
            .HasConversion(
                phone => phone == null ? null : phone.Value,
                value => value == null ? null : PhoneNumber.Create(value).Value)
            .HasMaxLength(20);

        builder.Property(c => c.Notes).HasMaxLength(2000);

        // Bag livre por tenant/segmento (convenio, raca do pet etc.) — ver
        // comentario em Customer.cs sobre nao validar schema ainda.
        builder.Property(c => c.CustomData)
            .HasConversion(
                data => JsonSerializer.Serialize(data, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
            .HasColumnType("jsonb");

        builder.Property(c => c.IsActive).IsRequired();

        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(c => new { c.TenantId, c.FullName });

        // Filtro de tenant + soft-delete fica no DbContext (ver AgendioDbContextBase).
    }
}
