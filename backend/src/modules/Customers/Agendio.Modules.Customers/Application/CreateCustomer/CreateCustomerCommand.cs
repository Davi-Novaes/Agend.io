using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.CreateCustomer;

// Sem TenantId: vem de ITenantContext (claim do JWT) — quem cria ja esta
// autenticado no tenant certo.
public sealed record CreateCustomerCommand(
    string FullName,
    string? Email,
    string? Phone,
    string? Notes,
    DateOnly? DateOfBirth,
    Dictionary<string, string>? CustomData) : ICommand<Guid>;
