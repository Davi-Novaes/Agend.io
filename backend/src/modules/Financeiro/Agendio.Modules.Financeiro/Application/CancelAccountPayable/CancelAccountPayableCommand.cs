using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.CancelAccountPayable;

public sealed record CancelAccountPayableCommand(Guid AccountPayableId) : ICommand;
