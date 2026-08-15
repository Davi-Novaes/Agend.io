using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.CancelWaitlistEntry;

public sealed record CancelWaitlistEntryCommand(Guid WaitlistEntryId) : ICommand;
