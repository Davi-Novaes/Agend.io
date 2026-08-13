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

    private readonly List<string> _specialties = [];

    private readonly List<Guid> _serviceIds = [];

    public TenantId TenantId { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public ResourceType Type { get; private set; }

    public int Capacity { get; private set; }

    public string? Description { get; private set; }

    public Guid? UnitId { get; private set; }

    /// <summary>Caminho publico do arquivo salvo por IFileStorage. Null usa um placeholder generico na UI. So faz sentido para Type == Person, mas nao e validado — nada impede uma sala de ter uma foto ilustrativa.</summary>
    public string? PhotoUrl { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<WorkingHoursEntry> WorkingHours => _workingHours;

    /// <summary>Tags livres (ex.: "Corte", "Coloracao") — usadas para o cliente entender o que o profissional faz. So faz sentido para Type == Person, mas nao e validado.</summary>
    public IReadOnlyCollection<string> Specialties => _specialties;

    /// <summary>
    /// Ids de Service (modulo Catalog, por isso Guid cru) que este recurso pode
    /// realizar. Lista vazia = SEM restricao, o recurso pode ser escalado para
    /// qualquer servico (default permissivo — tenant nao configurado nao perde
    /// funcionalidade). Ainda NAO usado como validacao bloqueante no
    /// agendamento — isso e responsabilidade da Fase 4.
    /// </summary>
    public IReadOnlyCollection<Guid> ServiceIds => _serviceIds;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private Resource()
    {
    }

    private Resource(TenantId tenantId, string name, ResourceType type, int capacity, string? description, Guid? unitId)
        : base(ResourceId.New())
    {
        TenantId = tenantId;
        Name = name;
        Type = type;
        Capacity = capacity;
        Description = description;
        UnitId = unitId;
        IsActive = true;
    }

    public static Result<Resource> Create(
        TenantId tenantId, string? name, ResourceType type, int capacity, string? description, Guid? unitId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Resource>(Error.Validation("Resource.NameEmpty", "O nome do recurso nao pode ser vazio."));
        }

        if (capacity <= 0)
        {
            return Result.Failure<Resource>(Error.Validation("Resource.InvalidCapacity", "A capacidade precisa ser maior que zero."));
        }

        var resource = new Resource(tenantId, name.Trim(), type, capacity, description?.Trim(), unitId);
        resource.Raise(new ResourceCreatedDomainEvent(resource.Id, tenantId, resource.Name));

        return Result.Success(resource);
    }

    public Result Update(string? name, ResourceType type, int capacity, string? description, Guid? unitId = null)
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
        UnitId = unitId;

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public Result SetPhoto(string photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return Result.Failure(Error.Validation("Resource.InvalidPhotoUrl", "URL da foto invalida."));
        }

        PhotoUrl = photoUrl;
        return Result.Success();
    }

    /// <summary>Substitui a lista inteira — o dono redefine tudo de uma vez, nao adiciona incrementalmente (mesmo padrao de SetWorkingHours).</summary>
    public void SetSpecialties(IReadOnlyList<string> specialties)
    {
        var normalized = specialties
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _specialties.Clear();
        _specialties.AddRange(normalized);
    }

    /// <summary>
    /// Substitui a lista inteira — o dono redefine tudo de uma vez (mesmo padrao
    /// de SetWorkingHours/SetSpecialties). Existencia/tenant de cada Id ja foi
    /// validada pelo handler via IServiceLookupService antes de chegar aqui —
    /// dominio nao tem acesso a lookup de outro modulo.
    /// </summary>
    public void SetServiceIds(IReadOnlyList<Guid> serviceIds)
    {
        _serviceIds.Clear();
        _serviceIds.AddRange(serviceIds.Distinct());
    }

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
