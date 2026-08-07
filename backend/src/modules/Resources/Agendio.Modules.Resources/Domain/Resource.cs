using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Resources.Domain;

/// <summary>
/// O que a agenda reserva de fato: uma pessoa (barbeiro, dentista, psicologo), um
/// espaco (sala, cadeira, box de lavagem) ou um equipamento. Capacity > 1 cobre
/// aula em grupo/academia (varios clientes no mesmo horario do mesmo recurso).
/// </summary>
public sealed class Resource : AggregateRoot<ResourceId>, ITenantOwned, IAuditable, ISoftDeletable
{
    private readonly List<WorkingHoursEntry> _workingHours = [];

    public TenantId TenantId { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public ResourceType Type { get; private set; }

    public int Capacity { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<WorkingHoursEntry> WorkingHours => _workingHours;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private Resource()
    {
    }

    private Resource(TenantId tenantId, string name, ResourceType type, int capacity, string? description)
        : base(ResourceId.New())
    {
        TenantId = tenantId;
        Name = name;
        Type = type;
        Capacity = capacity;
        Description = description;
        IsActive = true;
    }

    public static Result<Resource> Create(TenantId tenantId, string? name, ResourceType type, int capacity, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Resource>(Error.Validation("Resource.NameEmpty", "O nome do recurso nao pode ser vazio."));
        }

        if (capacity <= 0)
        {
            return Result.Failure<Resource>(Error.Validation("Resource.InvalidCapacity", "A capacidade precisa ser maior que zero."));
        }

        var resource = new Resource(tenantId, name.Trim(), type, capacity, description?.Trim());
        resource.Raise(new ResourceCreatedDomainEvent(resource.Id, tenantId, resource.Name));

        return Result.Success(resource);
    }

    public Result Update(string? name, ResourceType type, int capacity, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("Resource.NameEmpty", "O nome do recurso nao pode ser vazio."));
        }

        if (capacity <= 0)
        {
            return Result.Failure(Error.Validation("Resource.InvalidCapacity", "A capacidade precisa ser maior que zero."));
        }

        Name = name.Trim();
        Type = type;
        Capacity = capacity;
        Description = description?.Trim();

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Substitui a semana inteira — o dono redefine tudo de uma vez, nao adiciona incrementalmente.</summary>
    public Result SetWorkingHours(IReadOnlyList<(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime)> entries)
    {
        var parsedEntries = new List<WorkingHoursEntry>(entries.Count);

        foreach (var entry in entries)
        {
            var entryResult = WorkingHoursEntry.Create(entry.DayOfWeek, entry.StartTime, entry.EndTime);
            if (entryResult.IsFailure)
            {
                return Result.Failure(entryResult.Error);
            }

            parsedEntries.Add(entryResult.Value);
        }

        _workingHours.Clear();
        _workingHours.AddRange(parsedEntries);

        return Result.Success();
    }
}
