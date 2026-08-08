using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.GetMfaStatus;

/// <summary>UserId vem da claim do JWT — a tela de Configuracoes/Seguranca usa isto pra saber se mostra "habilitar" ou "desabilitar".</summary>
public sealed record GetMfaStatusQuery(Guid UserId) : IQuery<MfaStatusResult>;

public sealed record MfaStatusResult(bool MfaEnabled);
