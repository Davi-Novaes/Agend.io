using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Billing.Domain;

/// <summary>
/// Catalogo de planos da plataforma — dado de referencia, nao de tenant. Por
/// isso nasce direto via `migrationBuilder.InsertData` na migration inicial
/// (existe em QUALQUER ambiente, nao so em Development) em vez de um seeder de
/// runtime como o PlatformAdminDevSeeder.
/// </summary>
public sealed class Plan : AggregateRoot<PlanId>
{
    public string Name { get; private set; } = string.Empty;

    public decimal PriceAmount { get; private set; }

    public string Currency { get; private set; } = "BRL";

    public BillingCycle BillingCycle { get; private set; }

    public bool IsActive { get; private set; }

    private Plan()
    {
    }

    public Plan(PlanId id, string name, decimal priceAmount, BillingCycle billingCycle) : base(id)
    {
        Name = name;
        PriceAmount = priceAmount;
        BillingCycle = billingCycle;
        IsActive = true;
    }
}
