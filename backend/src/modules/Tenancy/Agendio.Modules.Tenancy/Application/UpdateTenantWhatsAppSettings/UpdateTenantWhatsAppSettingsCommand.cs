using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantWhatsAppSettings;

/// <summary>
/// AccessToken null/vazio significa "nao alterar o token ja salvo" — a API
/// nunca ecoa o token de volta (ver GetTenantProfileQuery), entao o frontend
/// nao tem como reenviar o valor atual; so envia um novo valor quando o dono
/// digita um. Ver UpdateTenantWhatsAppSettingsCommandHandler.
/// </summary>
public sealed record UpdateTenantWhatsAppSettingsCommand(
    bool Enabled,
    string? PhoneNumberId,
    string? AccessToken,
    string? ScheduledTemplate,
    string? ReminderTemplate,
    string? CancelledTemplate,
    string? RescheduledTemplate,
    string? ConfirmedTemplate,
    string? CompletedTemplate) : ICommand;
