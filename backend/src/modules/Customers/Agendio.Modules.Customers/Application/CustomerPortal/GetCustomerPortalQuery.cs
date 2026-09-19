using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed record GetCustomerPortalQuery(Guid TenantId, string SessionToken)
    : IQuery<CustomerPortalProfile>, IHasExplicitTenant;

public sealed record CustomerPortalProfile(
    string FullName,
    string Email,
    string? Phone,
    int LoyaltyPoints,
    int? LoyaltyVisitsForReward,
    string? LoyaltyRewardDescription,
    IReadOnlyList<CustomerPortalAppointment> Appointments);

public sealed record CustomerPortalAppointment(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string ResourceName,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    decimal Price,
    string Currency,
    string Status);
