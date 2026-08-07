using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Catalog.Application.CreateService;

public sealed record CreateServiceCommand(
    string Name, string? Description, int DurationMinutes, decimal Price, string Currency, string? Category) : ICommand<Guid>;
