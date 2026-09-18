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

    // 3 planos pagos reais (substituem o "Padrão" na vitrine) — mesmo raciocinio de Id fixo.
    public static readonly PlanId EssencialPlanId = PlanId.From(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    public static readonly PlanId ProfissionalPlanId = PlanId.From(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    public static readonly PlanId PremiumPlanId = PlanId.From(Guid.Parse("55555555-5555-5555-5555-555555555555"));

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
        builder.Property(p => p.MaxUnits);
        builder.Property(p => p.MaxProfessionals);
        builder.Property(p => p.MaxCustomers);
        builder.Property(p => p.IsFeatured).IsRequired();

        // Catalogo real desde a migration inicial — nao e dado de Development.
        // "Padrao" e "Gratis" desativados (nao deletados — assinaturas
        // existentes continuam com FK valida) em favor dos 3 planos pagos com
        // limite real abaixo. Gratis saiu porque nao fazia sentido oferecer
        // tudo ilimitado de graca ao lado de planos pagos com limite de verdade.
        builder.HasData(
            new Plan(DefaultPlanId, "Padrão", 99.00m, BillingCycle.Monthly, isActive: false),
            new Plan(FreePlanId, "Grátis", 0.00m, BillingCycle.Monthly, isActive: false),
            new Plan(EssencialPlanId, "Essencial", 49.99m, BillingCycle.Monthly, maxUnits: 1, maxProfessionals: 3, maxCustomers: 300),
            new Plan(ProfissionalPlanId, "Profissional", 69.99m, BillingCycle.Monthly, maxUnits: 3, maxProfessionals: 10, maxCustomers: 1500, isFeatured: true),
            new Plan(PremiumPlanId, "Premium", 99.99m, BillingCycle.Monthly, maxUnits: 10, maxProfessionals: 30, maxCustomers: null));
    }
}
