using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.SetProductActiveStatus;

public sealed record SetProductActiveStatusCommand(Guid ProductId, bool IsActive) : ICommand;
