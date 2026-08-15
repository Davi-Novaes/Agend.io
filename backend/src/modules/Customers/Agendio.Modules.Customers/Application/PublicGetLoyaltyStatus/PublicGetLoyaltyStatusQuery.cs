using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Customers.Application.PublicGetLoyaltyStatus;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record PublicGetLoyaltyStatusQuery(Guid TenantId, string Email)
    : IQuery<PublicLoyaltyStatus>, IHasExplicitTenant;

public sealed record PublicLoyaltyStatus(string CustomerName, int LoyaltyPoints, int LoyaltyVisitsForReward, string LoyaltyRewardDescription);
