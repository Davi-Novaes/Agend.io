using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Catalog.Domain;

/// <summary>Servico oferecido por UM tenant — o que aparece pra escolher ao criar um agendamento (Sprint 3).</summary>
public sealed class Service : AggregateRoot<ServiceId>, ITenantOwned, IAuditable, ISoftDeletable
{
    public TenantId TenantId { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Duracao real do agendamento = esta duracao + buffers, quando o motor de agenda existir (Sprint 3).</summary>
    public int DurationMinutes { get; private set; }

    public Money Price { get; private set; } = null!;

    public string? Category { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private Service()
    {
    }

    private Service(TenantId tenantId, string name, string? description, int durationMinutes, Money price, string? category)
        : base(ServiceId.New())
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        DurationMinutes = durationMinutes;
        Price = price;
        Category = category;
        IsActive = true;
    }

    public static Result<Service> Create(
        TenantId tenantId, string? name, string? description, int durationMinutes, decimal price, string currency, string? category)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Service>(Error.Validation("Service.NameEmpty", "O nome do servico nao pode ser vazio."));
        }

        if (durationMinutes <= 0)
        {
            return Result.Failure<Service>(Error.Validation("Service.InvalidDuration", "A duracao precisa ser maior que zero."));
        }

        var priceResult = Money.Create(price, currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure<Service>(priceResult.Error);
        }

        var service = new Service(tenantId, name.Trim(), description?.Trim(), durationMinutes, priceResult.Value, category?.Trim());
        service.Raise(new ServiceCreatedDomainEvent(service.Id, tenantId, service.Name));

        return Result.Success(service);
    }

    public Result Update(string? name, string? description, int durationMinutes, decimal price, string currency, string? category)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("Service.NameEmpty", "O nome do servico nao pode ser vazio."));
        }

        if (durationMinutes <= 0)
        {
            return Result.Failure(Error.Validation("Service.InvalidDuration", "A duracao precisa ser maior que zero."));
        }

        var priceResult = Money.Create(price, currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure(priceResult.Error);
        }

        Name = name.Trim();
        Description = description?.Trim();
        DurationMinutes = durationMinutes;
        Price = priceResult.Value;
        Category = category?.Trim();

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
