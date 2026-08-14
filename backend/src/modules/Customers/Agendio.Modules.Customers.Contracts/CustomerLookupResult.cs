namespace Agendio.Modules.Customers.Contracts;

public sealed record CustomerLookupResult(Guid CustomerId, string FullName, string? Email, string? Phone, bool IsActive);
