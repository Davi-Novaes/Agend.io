namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>Quais lembretes automaticos o tenant quer receber (Fase 7) — lido pelo Scheduling antes de disparar cada job.</summary>
public sealed record TenantNotificationSettings(bool Reminder24hEnabled, bool Reminder2hEnabled, bool PostServiceThankYouEnabled);
