using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantReminderSettings;

public sealed record UpdateTenantReminderSettingsCommand(bool Reminder24hEnabled, bool Reminder2hEnabled, bool PostServiceThankYouEnabled)
    : ICommand;
