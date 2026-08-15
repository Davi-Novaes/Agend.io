using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Scheduling.Application.PublicJoinWaitlist;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record PublicJoinWaitlistCommand(
    Guid TenantId,
    Guid ServiceId,
    Guid? ResourceId,
    DateOnly PreferredDate,
    string CustomerFullName,
    string CustomerEmail,
    string? CustomerPhone,
    string? Notes) : ICommand<Guid>, IHasExplicitTenant;
