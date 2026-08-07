namespace Agendio.Modules.Catalog.Contracts;

public sealed record ServiceLookupResult(
    Guid ServiceId, string Name, int DurationMinutes, decimal Price, string Currency, bool IsActive);
