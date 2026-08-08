using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.MarkAccountReceivableReceived;

public sealed record MarkAccountReceivableReceivedCommand(Guid AccountReceivableId) : ICommand;
