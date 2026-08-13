using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Resources.Application.SetResourceSpecialties;

public sealed record SetResourceSpecialtiesCommand(Guid ResourceId, IReadOnlyList<string> Specialties) : ICommand;
