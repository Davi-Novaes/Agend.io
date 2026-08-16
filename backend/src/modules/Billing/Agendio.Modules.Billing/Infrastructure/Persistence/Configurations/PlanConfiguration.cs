using Agendio.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    // Fixo (nao Guid.NewGuid()) para o seed ser reproduzivel entre ambientes —
    // e o mesmo Id em toda instalacao que rodar esta migration.
    public static readonly PlanId DefaultPlanId = PlanId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    // Mesmo raciocinio do DefaultPlanId acima — Id fixo e reproduzivel.
    public static readonly PlanId FreePlanId = PlanId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => PlanId.From(value)).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.PriceAmount).HasColumnType("numeric(10,2)");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.BillingCycle).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.IsActive).IsRequired();

        // Catalogo real desde a migration inicial — nao e dado de Development.
        builder.HasData(
            new Plan(DefaultPlanId, "Padrão", 99.00m, BillingCycle.Monthly),
            new Plan(FreePlanId, "Grátis", 0.00m, BillingCycle.Monthly));
    }
}
