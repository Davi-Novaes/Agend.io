using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que outro modulo tem sobre Tenancy — a forma
/// permitida de consultar dado de outro modulo (ver regra de dependencia em
/// CLAUDE.md). Nunca expõe o agregado Tenant nem o DbContext de Tenancy.
/// </summary>
public interface ITenantLookupService
{
    Task<TenantLookupResult?> FindByIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Horario de funcionamento, datas fechadas e intervalo entre agendamentos — usado pelo motor de disponibilidade (Scheduling, Fase 4).</summary>
    Task<TenantAvailabilityInfo?> GetAvailabilityInfoAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>Credenciais e templates da integracao com WhatsApp — usado pelo Scheduling para notificar o cliente (Fase 6).</summary>
    Task<TenantWhatsAppSettings?> GetWhatsAppSettingsAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>Quais lembretes automaticos estao ligados — usado pelo Scheduling antes de cada envio (Fase 7).</summary>
    Task<TenantNotificationSettings?> GetNotificationSettingsAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>Configuracao do programa de fidelidade — usado pelo Customers ao creditar/resgatar pontos (Fase 11).</summary>
    Task<TenantLoyaltySettings?> GetLoyaltySettingsAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
