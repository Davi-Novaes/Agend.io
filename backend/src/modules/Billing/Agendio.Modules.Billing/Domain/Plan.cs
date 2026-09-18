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

    /// <summary>Numero maximo de unidades/profissionais/clientes que o plano permite — null = sem limite.</summary>
    public int? MaxUnits { get; private set; }

    public int? MaxProfessionals { get; private set; }

    public int? MaxCustomers { get; private set; }

    /// <summary>So decorativo (badge "Mais popular") — nao carrega nenhuma regra de negocio.</summary>
    public bool IsFeatured { get; private set; }

    private Plan()
    {
    }

    public Plan(
        PlanId id,
        string name,
        decimal priceAmount,
        BillingCycle billingCycle,
        bool isActive = true,
        int? maxUnits = null,
        int? maxProfessionals = null,
        int? maxCustomers = null,
        bool isFeatured = false) : base(id)
    {
        Name = name;
        PriceAmount = priceAmount;
        BillingCycle = billingCycle;
        IsActive = isActive;
        MaxUnits = maxUnits;
        MaxProfessionals = maxProfessionals;
        MaxCustomers = maxCustomers;
        IsFeatured = isFeatured;
    }
}
