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

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string? Country { get; private set; }

    public string? Phone { get; private set; }

    public string? WhatsApp { get; private set; }

    private readonly List<BusinessHoursEntry> _businessHours = [];

    public IReadOnlyCollection<BusinessHoursEntry> BusinessHours => _businessHours;

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

    private Unit(TenantId tenantId, string name, string? address, string? city, string? state, string? country, string? phone, string? whatsApp)
        : base(UnitId.New())
    {
        TenantId = tenantId;
        Name = name;
        Address = address;
        City = city;
        State = state;
        Country = country;
        Phone = phone;
        WhatsApp = whatsApp;
        IsActive = true;
    }

    public static Result<Unit> Create(TenantId tenantId, string? name, string? address, string? city, string? state, string? country, string? phone = null, string? whatsApp = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Unit>(Error.Validation("Unit.NameEmpty", "O nome da unidade nao pode ser vazio."));
        }

        return Result.Success(new Unit(tenantId, name.Trim(), address?.Trim(), city?.Trim(), state?.Trim(), country?.Trim(), phone?.Trim(), whatsApp?.Trim()));
    }

    public Result Update(string? name, string? address, string? city, string? state, string? country, string? phone = null, string? whatsApp = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("Unit.NameEmpty", "O nome da unidade nao pode ser vazio."));
        }

        Name = name.Trim();
        Address = address?.Trim();
        City = city?.Trim();
        State = state?.Trim();
        Country = country?.Trim();
        Phone = phone?.Trim();
        WhatsApp = whatsApp?.Trim();

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public Result SetBusinessHours(IReadOnlyList<(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime)> entries)
    {
        var parsedEntries = new List<BusinessHoursEntry>(entries.Count);
        foreach (var entry in entries)
        {
            var result = BusinessHoursEntry.Create(entry.DayOfWeek, entry.StartTime, entry.EndTime);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            parsedEntries.Add(result.Value);
        }

        _businessHours.Clear();
        _businessHours.AddRange(parsedEntries);
        return Result.Success();
    }
}
