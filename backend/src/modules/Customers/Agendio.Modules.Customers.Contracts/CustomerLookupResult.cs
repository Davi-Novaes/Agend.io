namespace Agendio.Modules.Customers.Contracts;

public sealed record CustomerLookupResult(Guid CustomerId, string FullName, string? Email, bool IsActive);
