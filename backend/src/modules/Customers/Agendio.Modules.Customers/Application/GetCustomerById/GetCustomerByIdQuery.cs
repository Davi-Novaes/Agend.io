using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid CustomerId) : IQuery<CustomerDetails>;

public sealed record CustomerDetails(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string? Notes,
    DateOnly? DateOfBirth,
    IReadOnlyDictionary<string, string> CustomData,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
