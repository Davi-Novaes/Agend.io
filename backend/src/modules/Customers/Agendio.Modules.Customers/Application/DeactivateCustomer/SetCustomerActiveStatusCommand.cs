using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.DeactivateCustomer;

public sealed record SetCustomerActiveStatusCommand(Guid CustomerId, bool IsActive) : ICommand;
