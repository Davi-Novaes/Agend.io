using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Catalog.Application.GetServiceById;

public sealed record GetServiceByIdQuery(Guid ServiceId) : IQuery<ServiceDetails>;

public sealed record ServiceDetails(
    Guid Id, string Name, string? Description, int DurationMinutes, decimal Price, string Currency, string? Category, bool IsActive);
