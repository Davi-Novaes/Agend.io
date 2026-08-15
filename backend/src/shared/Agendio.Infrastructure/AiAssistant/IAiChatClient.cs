namespace Agendio.Infrastructure.AiAssistant;

/// <summary>
/// Abstracao de chat com tool-calling sobre o provedor de IA configurado (Fase 22
/// — Assistente Agend.io). Global (uma unica chave paga pela plataforma para
/// todos os tenants, com rate limiting agressivo por tenant), por isso a config
/// vem de IOptions — mesmo raciocinio de IPaymentChargeClient, ao contrario de
/// IWhatsAppSender, que e por tenant.
/// </summary>
public interface IAiChatClient
{
    Task<AiChatResult> SendAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}
