using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.SendCustomerMessage;

public sealed record SendCustomerMessageCommand(Guid CustomerId, string Subject, string Body) : ICommand;
