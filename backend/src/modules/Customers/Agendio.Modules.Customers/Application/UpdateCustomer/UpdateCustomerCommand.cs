using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string FullName,
    string? Email,
    string? Phone,
    string? Notes,
    DateOnly? DateOfBirth,
    Dictionary<string, string>? CustomData,
    string? Cpf = null,
    string? HealthNotes = null) : ICommand;
