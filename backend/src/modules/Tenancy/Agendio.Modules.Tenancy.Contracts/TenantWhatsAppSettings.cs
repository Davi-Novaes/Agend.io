namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>
/// Configuracao de integracao com a WhatsApp Cloud API (Fase 6) — lida pelo
/// Scheduling na hora de decidir se/como enviar uma notificacao. Templates null
/// usam o texto padrao definido em Scheduling (ver WhatsAppMessageDefaults).
/// </summary>
public sealed record TenantWhatsAppSettings(
    bool Enabled,
    string? PhoneNumberId,
    string? AccessToken,
    string? ScheduledTemplate,
    string? ReminderTemplate,
    string? CancelledTemplate,
    string? RescheduledTemplate,
    string? ConfirmedTemplate,
    string? CompletedTemplate);
