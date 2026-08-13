using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.DeleteTimeOff;

public sealed record DeleteTimeOffCommand(Guid TimeOffId) : ICommand;
