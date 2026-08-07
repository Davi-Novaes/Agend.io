using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.SetWorkingHours;

public sealed record SetResourceWorkingHoursCommand(Guid ResourceId, IReadOnlyList<WorkingHourEntryDto> Entries) : ICommand;
