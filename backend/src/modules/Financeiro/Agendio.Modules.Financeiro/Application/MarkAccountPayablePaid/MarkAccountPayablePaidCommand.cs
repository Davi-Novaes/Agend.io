using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.MarkAccountPayablePaid;

public sealed record MarkAccountPayablePaidCommand(Guid AccountPayableId) : ICommand;
