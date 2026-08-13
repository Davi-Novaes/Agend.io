using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Catalog.Application.UpdateService;

public sealed record UpdateServiceCommand(
    Guid ServiceId, string Name, string? Description, int DurationMinutes, decimal Price, string Currency, string? Category,
    int DisplayOrder) : ICommand;
