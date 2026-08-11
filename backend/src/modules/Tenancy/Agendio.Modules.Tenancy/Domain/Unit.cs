using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Uma unidade fisica do estabelecimento (loja, filial, endereco). Tenant de
/// unidade unica nunca precisa cadastrar nenhuma — Resource/Appointment tratam
/// UnitId como opcional em todo o sistema.
/// </summary>
public sealed class Unit : AggregateRoot<UnitId>, ITenantOwned, IAuditable, ISoftDeletable
{
    public TenantId TenantId { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private Unit()
    {
    }

    private Unit(TenantId tenantId, string name, string? address)
        : base(UnitId.New())
    {
        TenantId = tenantId;
        Name = name;
        Address = address;
        IsActive = true;
    }

    public static Result<Unit> Create(TenantId tenantId, string? name, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Unit>(Error.Validation("Unit.NameEmpty", "O nome da unidade nao pode ser vazio."));
        }

        return Result.Success(new Unit(tenantId, name.Trim(), address?.Trim()));
    }

    public Result Update(string? name, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("Unit.NameEmpty", "O nome da unidade nao pode ser vazio."));
        }

        Name = name.Trim();
        Address = address?.Trim();

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
