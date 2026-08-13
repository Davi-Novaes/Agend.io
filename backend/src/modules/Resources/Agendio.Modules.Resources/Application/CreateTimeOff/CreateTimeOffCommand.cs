using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.CreateTimeOff;

public sealed record CreateTimeOffCommand(Guid ResourceId, DateOnly StartDate, DateOnly EndDate, string? Reason) : ICommand<Guid>;
