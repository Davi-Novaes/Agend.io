using Agendio.Modules.Resources.Domain;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Resources.Infrastructure.Persistence.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => ResourceId.From(value))
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Capacity).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);

        // Guid cru, sem FK de banco (cross-schema, mesmo motivo de
        // Appointment.ResourceId nao ter FK para resources.resources).
        builder.Property(r => r.UnitId);

        builder.Property(r => r.IsActive).IsRequired();

        // Colecao de Value Objects sem identidade propria — tabela filha, FK
        // sombra gerada pelo EF, sem repositorio separado (faz parte do agregado
        // Resource, nunca acessada isoladamente).
        builder.OwnsMany(r => r.WorkingHours, workingHours =>
        {
            workingHours.ToTable("resource_working_hours");
            workingHours.WithOwner().HasForeignKey("resource_id");
            workingHours.Property<int>("id");
            workingHours.HasKey("id");

            workingHours.Property(w => w.DayOfWeek).HasConversion<string>().HasMaxLength(20).IsRequired();
            workingHours.Property(w => w.StartTime).IsRequired();
            workingHours.Property(w => w.EndTime).IsRequired();
        });

        builder.Property(r => r.CreatedBy).HasMaxLength(256);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(r => new { r.TenantId, r.Name });

        // Filtro de tenant + soft-delete fica no DbContext (ver AgendioDbContextBase).
    }
}
