using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPaymentSettings;

public sealed record UpdateTenantPaymentSettingsCommand(bool PaymentRequired, int DepositPercentage) : ICommand;
