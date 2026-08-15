using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantLoyaltySettings;

public sealed record UpdateTenantLoyaltySettingsCommand(bool LoyaltyProgramEnabled, int LoyaltyVisitsForReward, string LoyaltyRewardDescription)
    : ICommand;
