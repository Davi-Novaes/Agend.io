using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Scheduling.Application.PublicScheduleAppointment;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record PublicScheduleAppointmentCommand(
    Guid TenantId,
    Guid ResourceId,
    Guid ServiceId,
    DateTimeOffset StartAtUtc,
    string CustomerFullName,
    string CustomerEmail,
    string? CustomerPhone,
    string? Notes) : ICommand<Guid>, IHasExplicitTenant;
