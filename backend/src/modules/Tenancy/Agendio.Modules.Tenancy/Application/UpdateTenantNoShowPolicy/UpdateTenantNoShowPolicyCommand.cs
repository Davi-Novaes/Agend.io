using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantNoShowPolicy;

public sealed record UpdateTenantNoShowPolicyCommand(bool RequireDepositAfterNoShows, int NoShowThresholdForDeposit)
    : ICommand;
