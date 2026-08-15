namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>Configuracao do programa de fidelidade (Fase 11) — lido pelo Customers ao creditar/resgatar pontos.</summary>
public sealed record TenantLoyaltySettings(bool LoyaltyProgramEnabled, int LoyaltyVisitsForReward, string LoyaltyRewardDescription);
