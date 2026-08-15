using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.RedeemCustomerLoyaltyReward;

public sealed record RedeemCustomerLoyaltyRewardCommand(Guid CustomerId) : ICommand;
